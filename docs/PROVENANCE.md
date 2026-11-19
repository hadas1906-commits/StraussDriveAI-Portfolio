# Provenance and exclusions

## Retained implementation

The owner-supplied private project implementation was selectively copied into a separate directory: C# financial arithmetic and eligibility services, conversation/goal/capability models, bounded orchestrator, SQLite persistence, API role/ownership/CSRF/rate-limit controls, React UI/charts, generic inline car SVG and synthetic authentication regression tests. Internal `StraussDrive` namespaces and `HackathonData` type names remain for code continuity; they do not imply use of supplied challenge data or an official endorsement.

This classification identifies project implementation; it does not independently prove legal title. The owner must confirm rights in the code/artwork and permission from any contributors before licensing/publication. Git metadata and possession of source are not proof of exclusive ownership.

## Newly authored for this edition

- `data/portfolio-demo.json`: six invented models from a fictional manufacturer, five explicitly named fictional profiles, three occupations and three budget groups; no personal identifiers, real manufacturer/model codes or real tax tables.
- Fictional year 2099, original salaries/costs/participation, progressive 10/20/30% tax example, 100-unit credit points, 1%/2% insurance, 5%/1% employee contributions, family threshold three up to age 20 and 40/60 gap split, 175-unit electric benefit, 900/1,350-unit waiver allowances.
- JSON loader, independent Python financial reference oracle, new financial/workflow/frontend/browser tests, local launcher, screenshots and bilingual portfolio documentation.

The retained generic mechanisms resemble the original problem domain; the rows, values and policy configuration are not transformed or renamed source records. Tests for this edition use its own independently authored expectations. No claim is made that invented financial tables are legally correct.

## Excluded completely, including from the fresh history

All supplied PDF/Excel/JSON/government CSV artifacts; source-specific import/rules parsers; original employee records, benchmark answers, source hashes, evaluation transcripts and historical result reports; old screenshots; cloud/deployment files; private-repository documentation and Git history; production URLs; secrets, databases and signing keys; external AI HTTP integration and paid/live evaluation runners.

No organizer permission has been assumed. Excluding the source documents does not establish ownership of every retained code contribution; that is a separate owner confirmation.

## Third-party components

npm/NuGet packages are restored rather than vendored. The locked dependency inventory and available license texts are in `THIRD_PARTY_NOTICES.md`. Heebo is OFL-1.1, Lucide ISC, and runtime libraries include MIT/ISC/Apache/BSD terms. MPL and CC-BY dependencies also appear in the toolchain; package-level terms and attribution must remain intact if redistributed. Original third-party copyright notices are preserved verbatim and are not personal data collected by this application.
