# Licensing review — proposal, not a license grant

## Project code

No project-code LICENSE or explicit redistribution grant was found in the source repository. The owner requested this separate portfolio, but that does not prove exclusive title, contributor consent, employment/IP terms or organizer terms. No project-code license has been applied here and publication remains pending approval.

**Recommended license: MIT** for the implementation and newly authored fictional examples, once the owner confirms the right to license them. MIT is a concise permissive license suitable for recruiter review and reuse, with copyright/permission notice retention and warranty disclaimer. It offers no separate explicit patent grant; consider Apache-2.0 if that is a project requirement.

Before applying MIT, confirm:

1. The owner has redistribution/licensing rights in retained code, inline artwork and the new dataset, including contributor or employer rights where applicable.
2. No binding challenge terms restrict publication of independently implemented code. Challenge-provided materials are excluded regardless.
3. The desired copyright holder/year and the final license choice. A repository made readable without a license does not automatically grant open-source reuse rights.

After explicit approval, add the standard MIT text as `LICENSE` for owner-controlled files and an ownership/scope notice excluding third-party materials. Do not overwrite, remove or claim ownership of upstream notices. This document is a proposal only.

## Third-party obligations

`THIRD_PARTY_NOTICES.md` inventories all 296 locked npm dependency entries and 49 restored NuGet packages, including direct/transitive and development dependencies. Available upstream license texts are preserved, and the Vite build emits the notices beside bundled JavaScript/fonts. Optional packages for other platforms are not all installed; their package metadata license is recorded, but their uninstalled package contents were not independently inspected. Most NuGet metadata declares MIT or Apache-2.0; `xunit.abstractions` has legacy license-URL metadata and is a test-only dependency. No dependency packages or compiled backend binaries are committed.

- **Heebo:** preserve SIL OFL 1.1 and copyright; do not sell the font alone or imply ownership. Font files are supplied by npm; no modification/renaming of the font is performed.
- **Lucide:** retain ISC notice for the icon implementation. No Strauss logo assets are included.
- **MIT/BSD/ISC/Apache dependencies:** retain required copyright/license/NOTICE material when distributing their code or binaries.
- **MPL toolchain components:** preserve applicable notices and source-availability obligations if redistributing covered binaries or modifying covered files; this edition does not vendor or modify those package sources.
- **CC-BY toolchain data:** retain upstream attribution/license material if redistributing that data. No such dataset is authored by this project.

The screenshot UI uses these third-party components; it does not turn their license into MIT. Existing JavaScript banners are retained, with the additional notices file. Before distributing binaries/containers or changing dependencies, review the exact resulting distribution again; this source-only review is not an exhaustive legal opinion or external compliance certification.
