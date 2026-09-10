# Data and asset licences

The source code in this repository is MIT licensed (see `LICENSE`). The datasets it
redistributes carry their own terms, listed here per BRD section 11 ("Bayrak/veri lisans
ihlali" risk). Every dataset file also records its own `source` and `license` fields, so the
provenance travels with the data rather than living only in this document.

| Asset | Location | Upstream | Licence |
|---|---|---|---|
| Provinces | `data/provinces.json` | Turkish address export, compiled for this project | MIT |
| Districts | `data/districts.json` | Turkish address export, compiled for this project | MIT |
| Quarters (semt) & postal codes | `data/quarters.json` | Turkish address export, compiled for this project | MIT |
| Neighbourhoods & villages | `data/neighborhoods.json` | Turkish address export, compiled for this project | MIT |
| Currencies | `data/currencies.json` | ICU, via the .NET runtime | Unicode-3.0 |
| Languages | `data/languages.json` | ICU, via the .NET runtime | Unicode-3.0 |
| Public holidays | `data/holidays.json` | Computed for this project | MIT |
| Mobile prefixes | `data/mobile-prefixes.json` | [google/libphonenumber](https://github.com/google/libphonenumber) | Apache-2.0 |
| Countries & calling codes | `data/countries.json` | ISO 3166-1 country codes + ITU-T E.164 calling codes, compiled for this project | MIT |
| Mobile operators | `data/mobile-operators.json` | Turkey's three licensed mobile network operators, compiled for this project | MIT |
| Banks (TCMB EFT codes) | `data/banks.json` | TCMB Ödeme Sistemleri Katılımcıları (2026) | MIT |
| Turkish currency names | `data/overrides/currencies.tr.json` | Written for this project | MIT |
| Holiday corrections | `data/overrides/holidays.tr.json` | Written for this project | MIT |

## Notes

**Province area codes (5 September 2026).** `provinces.json` now carries `areaCodes`, the
landline "alan kodu" per province — three digits, no trunk "0". This is the BTK national
numbering plan, a stable government assignment with no copyright of its own, the same
reasoning as the plate codes already in the file; İstanbul is the only province with two
(`212` Avrupa Yakası, `216` Anadolu Yakası). It was part of the original BRD §3.1 scope but was
silently dropped during the 2 September 2026 move to the curated address export (see "No
population, area or coordinates" below, which documented the fields that were dropped
*deliberately* — this one wasn't, and PLAN.md's Faz 0/C3 status lines claimed it as done when
it no longer was; both have been corrected). It is added back by hand here, independent of
the source export, because the export carries no telephony fields at all.

**Mobile operators — MIT, self-compiled (5 September 2026).** Turkey's three licensed mobile
network operators (Turkcell, Vodafone, Türk Telekom) — names, not a database anyone holds
copyright over. Deliberately **not** linked to `mobile-prefixes.json`: see that dataset's own
entry below for why a prefix→operator mapping isn't published, which applies just as much in
this direction. This file exists so "which operators does Turkey have" has an answer without
resurrecting that mapping.

**Banks and EFT codes — MIT, transcribed from TCMB (10 September 2026).** `banks.json` is a
row-for-row transcription of TCMB's own *Ödeme Sistemleri Katılımcıları (2026)* list: 71 rows,
in TCMB's order, with `legalName` carried verbatim so any row can be checked against the
published list character for character. A participant code and a registered company name are
administrative assignments, not creative works — the same reasoning as the address hierarchy
and the calling codes — so the compilation is MIT like the rest of the repository.

The EFT code is the reason to publish this at all: characters 5-9 of every Turkish IBAN are
that code, left-padded to five digits, which is what `/api/v1/banks/by-iban/{iban}` reads.

*Two rows are not banks.* Merkezi Kayıt Kuruluşu (`0806`) and PTT (`0807`) are payment-system
participants rather than banks. They are kept, marked `type: "Other"`, because dropping them
would leave holes in a code-to-institution map whose whole value is being complete.

*`type` is added here, not transcribed.* TCMB's list carries no category, so the BDDK licence
category is filled in per row from the registered name, which under Turkish banking naming
rules states it: "KATILIM BANKASI" → `Participation`, "YATIRIM"/"KALKINMA" → `DevelopmentInvestment`,
`0001` → `CentralBank`, the two rows above → `Other`, everything else → `Deposit` (digital-only
banks included — FUPS Bank, Colendi Bank, Ziraat Dinamik and Enpara all hold digital *deposit*
banking licences). Exactly three rows are development and investment banks whose names say
neither word — İller Bankası (`0004`), Türk Eximbank (`0016`) and Takasbank (`0132`) — and they
are corrected by hand. The data tests assert the rule *and* those three exceptions, so a future
row that quietly breaks the pattern fails CI instead of shipping with the wrong category.

*`name` is editorial, `legalName` is authoritative.* "Garanti BBVA" for "T. GARANTİ BANKASI
A.Ş." is this project's display choice, made because that is what the institution calls itself
today; a caller that needs the string a regulator would recognise wants `legalName`, which is
never touched.

*No BIC/SWIFT field, deliberately.* PLAN.md scoped D3 as "EFT/SWIFT codes" and the SWIFT half
is not shipped. SWIFT's own BIC directory is a licensed product, and the free aggregator lists
that stand in for it are demonstrably corrupt — one consulted while compiling this file listed
Akbank's `AKBKTRIS` against a different bank entirely. A caller cannot tell a wrong BIC from a
right one, and a wrong BIC misroutes money, so no field is better than a field that is right
most of the time. Adding one later from per-bank published sources is purely additive.

**Turkish address hierarchy — MIT.** The four levels — il, ilçe, semt, mahalle/köy — were
compiled for this project from one Turkish-language export and are published here under MIT
along with the rest of the repository. The underlying facts are administrative divisions and
PTT postal code assignments, which are matters of public record and carry no copyright of
their own; what is licensed is this compilation of them.

**These four files are the source of truth, not a build artefact.** The export they were
generated from is not committed — it is 16 MB that would sit beside the 21 MB it produces, to
be read by nobody. `tools/YaMiSoFt.OpenData.DataTool -- turkey --source <dir>` still performs
the conversion for whoever holds a copy, but the normal way to change this data is to change
the committed JSON and let the integrity tests check it. Those tests are what the auditability
now rests on: they re-derive every slug from its name, re-check every denormalized parent
name, every declared child count and every postal code against the file it came from, so a
hand edit that contradicts the rest of the hierarchy fails CI rather than shipping.

**Previously: ubeydeozdmr/turkiye-api (MIT).** Until 2 September 2026 the province, district
and settlement files were derived from
[ubeydeozdmr/turkiye-api](https://github.com/ubeydeozdmr/turkiye-api), which republishes TÜİK
and Ministry of Interior figures. None of that data is redistributed here any more.

**No population, area or coordinates.** Those turkiye-api files carried TÜİK population and
area figures for provinces and districts. They were dropped rather than carried across:
serving a 2022 population figure attached to a 2026 district identifier would have published
two vintages as one record with no way for a caller to tell them apart — and the district
identifiers are not shared between the two sources, so the join could not have been made
honestly in the first place.

**Currencies and languages — Unicode-3.0.** These are derived at generation time from the ICU
tables that ship inside .NET, not downloaded from anywhere. ICU is distributed under the
Unicode licence, which permits redistribution of the data with attribution. There is no
network dependency and nothing to go stale on someone else's schedule.

**Public holidays — computed.** Fixed national dates are matters of Turkish law and carry no
copyright. Religious dates are derived from the Umm al-Qura calendar. **They are not an
official source**: Diyanet İşleri Başkanlığı and the Resmî Gazete are. The dates have matched
for every recent year, and `data/overrides/holidays.tr.json` exists to correct any that
diverge — but anyone relying on them for a legal or payroll obligation should verify against
the official calendar.

**Mobile prefixes — Apache-2.0.** Derived from libphonenumber's metadata. Apache-2.0 permits
redistribution with attribution, which this file provides. The values themselves are BTK
allocations — facts, not authorship.

**Postal codes.** Codes are assigned per quarter (semt), and the dataset carries exactly one
code per quarter. Every settlement republishes the code of the quarter above it; that
denormalization is checked against its source in the data tests, so the two can never drift.

**Countries & calling codes — MIT, self-compiled (5 September 2026).** Deliberately not
sourced from any third-party countries package — see "Previously carried, now removed" below
for why the last one was dropped. ISO 3166-1 alpha-2/alpha-3 codes and ITU-T E.164 assigned
calling codes are administrative assignments, not creative works, so they carry no copyright
of their own (the same reasoning as the Turkish address hierarchy and the mobile prefixes
above); what is licensed under this project's MIT is the compilation, which is why the dataset
can be MIT rather than needing to inherit anyone else's share-alike terms. Two calling-code
values are intentionally shared by two rows each — `"1"` (United States and Canada) and `"7"`
(Russia and Kazakhstan) are real ITU-T assignments with no calling-code-level way to tell the
pair apart; every other Nanpa member carries its distinguishing area code instead (e.g.
`"1242"` for the Bahamas) so it stays unique. `XK` (Kosovo) is included with calling code
`"383"` even though it is not part of the formal ISO 3166-1 standard, because it is a real,
dialable country and omitting it would be its own kind of inaccuracy; this is noted here rather
than left implicit.

**Previously carried, now removed.** Three datasets were dropped rather than kept, each for
its own reason, and are recorded here so this file still says what the repository once
distributed:

- **Countries** (`data/countries.json`) — [mledoze/countries](https://github.com/mledoze/countries),
  ODbL-1.0. ODbL is share-alike: redistributing the database publicly requires offering it
  under ODbL too. That obligation is easy to satisfy while it is carried, but it is a
  condition this repository no longer wants to hold itself to, so the dataset and the
  `/api/v1/countries` endpoints were removed rather than kept under it. **Reintroduced 5
  September 2026** at the same path and route, this time compiled directly from ISO 3166-1 and
  ITU-T E.164 (public facts, no upstream package, no share-alike obligation) — see the MIT
  entry and note above.
- **Flags** (`assets/flags/`) — [lipis/flag-icons](https://github.com/lipis/flag-icons),
  MIT. Not a licence problem on its own — MIT permitted the redistribution and the
  rasterization step it received. It was removed because the flag endpoint identified a
  country by resolving it through `countries.json`; once that dataset was gone the feature
  had nothing to key off, and re-keying it by ISO alpha-2 directly was judged not worth
  keeping the artwork (7.3 MB of SVG/PNG) for.
- **Time zones** (`data/timezones.json`) — IANA tz database (`zone1970.tab`) + ICU, public
  domain / Unicode-3.0. Removed alongside the above as part of the same licence-surface
  reduction; nothing else in the dataset depended on it.

**Anything added later** must be public domain, CC0, MIT, or ODbL, and must be recorded in
this table in the same pull request that adds it. Datasets under non-commercial or
share-alike-beyond-ODbL terms are out of scope for this project.

## Not redistributed

Per BRD section 3.2, this project carries no personal data, no real-time financial data, and
no third-party data under paid or restricted licences.
