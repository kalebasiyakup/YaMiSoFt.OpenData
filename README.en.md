# OpenData API

**[Türkçe](README.md)**

Free, fast, documented reference data for developers — the full Turkish address hierarchy,
currencies, languages and public holidays. No signup required.

Built with .NET 9 minimal APIs. All data is committed to this repository, loaded into memory
at startup, and served from immutable collections: no database, no external calls at runtime.

> Status: **Faz 2** — the Turkish address hierarchy (81 provinces, 972 districts, 2 433
> quarters, 73 552 neighbourhoods and villages), postal codes, currencies, languages and
> public holidays.
> See [docs/PLAN.md](docs/PLAN.md) for the roadmap and
> [acik-veri-api-brd.md](acik-veri-api-brd.md) for the business requirements (Turkish).

## Quick start

```bash
dotnet run --project src/YaMiSoFt.OpenData.Api
```

Then open `/docs` for the interactive API reference.

```bash
# Turkish provinces — by plate code, slug, or display name
curl localhost:5096/api/v1/provinces/34
curl localhost:5096/api/v1/provinces/kahramanmaras
curl "localhost:5096/api/v1/provinces/34/districts"

# Search is case- and diacritic-insensitive everywhere
curl "localhost:5096/api/v1/districts?search=besiktas"

# Currencies carry the minor-unit count, so money rounds correctly
curl localhost:5096/api/v1/currencies/JPY     # decimalDigits: 0
curl localhost:5096/api/v1/currencies/KWD     # decimalDigits: 3

# Only the fields you need
curl "localhost:5096/api/v1/provinces?fields=id,name,districtCount&sort=districts&order=desc"

# The whole dataset in one request; cache it locally
curl localhost:5096/api/v1/provinces/all

# Public holidays, including the calculated religious ones
curl localhost:5096/api/v1/holidays/2026

# Down the address hierarchy: province -> district -> quarter -> settlement
curl "localhost:5096/api/v1/districts/430/quarters"
curl "localhost:5096/api/v1/quarters/1154/neighborhoods"

# Postal codes are assigned per quarter, so a code resolves to exactly one
curl localhost:5096/api/v1/postal-codes/34357
curl localhost:5096/api/v1/postal-codes/34357/neighborhoods
```

## Endpoints

Every list endpoint accepts the same parameters: `page`, `pageSize` (max 500), `search`,
`sort`, `order`, `fields`, `lang`. Every one of them has an `/all` sibling that returns the
complete dataset in a single response.

| Route | Notes |
|---|---|
| `/api/v1/provinces` · `/{idOrSlug}` | Plate code, slug or display name. Sort by `name`, `id`, `districts` |
| `/api/v1/provinces/{idOrSlug}/districts` | The districts of one province |
| `/api/v1/districts` · `/{id}` | Sort by `name`, `id`, `province`, `quarters` |
| `/api/v1/districts/{id}/quarters` | The quarters (semt) of one district |
| `/api/v1/quarters` · `/{id}` | Filter by `provinceId` or `districtId`. Sort by `name`, `id`, `postalCode`, `settlements` |
| `/api/v1/quarters/{id}/neighborhoods` | The settlements of one quarter |
| `/api/v1/currencies` · `/{code}` | ISO 4217, with symbol and minor units |
| `/api/v1/languages` · `/{code}` | ISO 639-1 or 639-2, with native names |
| `/api/v1/holidays/{year}` | Turkey's non-working days, 2015-2050 |
| `/api/v1/neighborhoods` · `/{id}` | Filter by `provinceId`, `districtId`, `quarterId`, `kind` or `search` |
| `/api/v1/districts/{id}/neighborhoods` | The settlements of one district |
| `/api/v1/postal-codes/{code}` | The quarter a five-digit code is assigned to |
| `/api/v1/postal-codes/{code}/neighborhoods` | The settlements covered by one code |
| `/health/live` · `/health/ready` | Liveness and readiness |
| `/metrics` | Prometheus scrape endpoint |
| `/docs` · `/openapi/v1.json` | Scalar UI and OpenAPI document |

Errors are [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457) problem documents.

## Rate limits

| Tier | Limit | Identified by |
|---|---|---|
| Anonymous | 30/min, 1 000/day | Client address |
| Free API key | 120/min, 10 000/day | `X-API-Key` header |
| Bulk download (`/all`) | 5/min, 100/day | Same, separate counter |

Limits are best effort, not a contract: the per-minute burst is enforced at the CDN edge, and
the daily figures are counted in the application.

Every response carries `X-RateLimit-Limit`, `X-RateLimit-Remaining` and `X-RateLimit-Reset`.
A rejection adds `Retry-After`.

**Please cache.** Responses are `public, max-age=86400` with ETags. If you need a whole
dataset, take `/all` once rather than walking the pages — it is cheaper for you and it is what
keeps this service free to run.

## Contributing data

Data lives in `data/` as JSON and is reviewed like code. To change it:

1. Edit the JSON, or regenerate it:
   ```bash
   dotnet run --project tools/YaMiSoFt.OpenData.DataTool -- all                    # every dataset
   dotnet run --project tools/YaMiSoFt.OpenData.DataTool -- turkey --source <dir>  # the whole TR hierarchy
   ```
2. Run the integrity suite: `dotnet test`
3. Open a PR. CI re-runs the same checks — uniqueness of codes, referential integrity down
   the whole province → district → quarter → settlement chain, slug safety, postal codes that
   match their province and are unique to one quarter, and agreement between every declared
   child count and the file below it.

The data tool is never run during a build. Its output is committed, so builds are
deterministic, work offline, and produce a container image that carries everything it serves.
Every data change is visible in the git history.

**The Turkish hierarchy edits like data, not like a build artefact.** The four address files
were generated from a Turkish-language export that is *not* committed — 16 MB of input beside
the 21 MB it produces, read by nobody. `datatool turkey --source <dir>` still converts it for
whoever holds a copy, and writes all four files together: regenerating one without the others
would produce a hierarchy whose identifiers no longer line up, so the command does not offer
that.

Normally you edit the committed JSON directly. The integrity suite is what keeps that safe —
it re-derives every slug from its name, and re-checks every denormalized parent name, every
declared child count and every postal code against the file it came from. An edit that
contradicts the rest of the hierarchy fails CI instead of shipping.

**Curated overlays.** Where a value cannot be derived from any upstream, it lives in
`data/overrides/` and is merged in at generation time. `currencies.tr.json` is the first of
these: ICU can name a currency in English and in its own home locale but not in Turkish, so
the Turkish names are hand-maintained. The data tool fails if an overlay names a code that no
longer exists, and the tests assert the same from the committed output.

## Deploying to Vercel

Vercel has no .NET runtime, but Vercel Functions run custom OCI container images, so
`Dockerfile.vercel` at the repository root is all that is needed. Two settings are not
optional in production:

```bash
# Without this, every request appears to come from Vercel's edge and the anonymous tier
# collapses into a single bucket for the entire world.
OpenData__Proxy__Provider=Vercel

# States that an edge rule handles bursts, so in-process counters are a deliberate choice.
OpenData__RateLimit__EdgeBurstProtection=true
```

Then add the edge rule, in **Project → Firewall → New Rule**:

| Field | Value |
|---|---|
| If | Request Path starts with `/api/` |
| Then | Rate Limit |
| Window / limit | 60s / 30 requests |
| Key | IP |
| Action | Deny (429) |

**Rate limiting is layered on purpose.** The edge rule is what actually protects the service:
it rejects abusive traffic before any compute is billed, and it counts accurately because the
edge sees every request. The application's own counters then cover what an edge rule cannot
express — the daily window, the per-tier allowances, and the `X-RateLimit-*` headers.

Those in-process counters are exact on a single instance and approximate once Vercel scales
out. That is an accepted trade: with the CDN absorbing most traffic, the origin sees a small
fraction of requests and usually runs one instance, and the moments when it does not are
exactly the moments the edge rule is doing the real work. If per-caller daily quotas ever need
to be exact — which the API key portal will require — set
`OpenData__RateLimit__RedisConnectionString` and counting moves to Redis with no behavioural
change; the window semantics are shared between both stores.

The application logs a warning at startup if neither an edge rule nor Redis is configured, and
reports the arrangement when one of them is. `appsettings.Vercel.json` carries the profile.

Everything else follows from the platform: the container reads its port from `PORT`, TLS
terminates at the edge so the app does not redirect to HTTPS, and responses carry
`CDN-Cache-Control` so the edge answers most requests without waking the container — which on
a scale-to-zero plan is the difference between paying for a request and not.

Instances scale to zero after five minutes without traffic. Process startup measures around
300 ms; a genuine cold boot adds container start on top, so the BRD's "P95 < 100 ms" target
holds for warm instances only.

## Known gaps

- **Religious holiday dates are computed**, not official. They have matched Diyanet's
  published dates every recent year, but Diyanet and the Resmî Gazete are the authority.
  Do not rely on them for a legal or payroll obligation without checking.
- **Mobile prefixes carry no operator.** Turkey has had number portability since 2008, so a
  prefix records the original allocation and not who carries a number today.
- **Settlements load on first use**, not at startup: parsing 73,552 rows costs roughly
  1-1.5 seconds, which would otherwise land on every cold start. The first request that needs
  them pays it.
- **Turkish currency names** cover the 43 currencies in the overlay; the rest fall back to
  their English name. Adding a row to `data/overrides/currencies.tr.json` is a one-line PR.
- **XML responses** (BRD FR-01) are deferred; the API is JSON only for now.
- **Countries, flags and time zones were removed.** They were part of earlier phases; see
  [DATA-LICENSES.md](DATA-LICENSES.md) for why.

## Licence

Code is [MIT](LICENSE). Data and images carry their own terms — see
[DATA-LICENSES.md](DATA-LICENSES.md).
