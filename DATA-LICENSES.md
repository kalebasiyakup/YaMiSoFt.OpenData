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
| Turkish currency names | `data/overrides/currencies.tr.json` | Written for this project | MIT |
| Holiday corrections | `data/overrides/holidays.tr.json` | Written for this project | MIT |

## Notes

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

**Previously carried, now removed.** Three datasets were dropped rather than kept, each for
its own reason, and are recorded here so this file still says what the repository once
distributed:

- **Countries** (`data/countries.json`) — [mledoze/countries](https://github.com/mledoze/countries),
  ODbL-1.0. ODbL is share-alike: redistributing the database publicly requires offering it
  under ODbL too. That obligation is easy to satisfy while it is carried, but it is a
  condition this repository no longer wants to hold itself to, so the dataset and the
  `/api/v1/countries` endpoints were removed rather than kept under it.
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
