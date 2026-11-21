# Public viewing without a project-code license

## Current decision

The owner wants recruiter access to source, screenshots and documentation, without a broad MIT reuse grant. **No MIT or other project-code license is applied or planned for this publication.** Public availability is not an open-source license. Publication remains subject to a separate explicit owner approval.

No project-code LICENSE or explicit redistribution grant was found in the source repository. The retained implementation is owner-supplied project code; Git possession and commit authorship cannot independently establish legal title.

## Contributor review

On 21 November 2026, the owner explicitly confirmed that all project code was developed personally with assistance from AI tools, including Codex, and that no other human developers contributed code. This resolves the previously unconfirmed human-contributor question on the basis of the owner's declaration. The two historical Git author identities are not treated as evidence of two human contributors or outstanding contributor permissions.

This records the owner's statement; it is not an independent legal determination of copyright title or of the copyrightability of AI-assisted material. Third-party dependencies remain separately licensed and are not claimed as owner-authored implementation.

### Remaining redistribution check

The supplied challenge instructions request source code as a submission deliverable, but no explicit assignment of independently written code or restriction on its public publication was identified in that document. Challenge datasets/documents are excluded, so no redistribution permission for those excluded files is being asserted or needed for this edition.

No separate participant agreement, employment/IP agreement or other publication contract was supplied. Its existence/applicability cannot be inferred from sole authorship. **Remaining contractual clearance condition:** the owner must confirm that no separate applicable agreement restricts public publication of the retained code/artwork, or resolve a restriction if one exists. This does not establish that organizer approval is always required. If no such agreement applies, no additional organizer permission has been identified as necessary for the reviewed source-only edition.

## What no license means

- Copyright protections generally remain with the respective rightsholders. Merely making a repository public does not grant broad rights to reuse, modify, commercially exploit or redistribute the owner-controlled implementation. Applicable legal exceptions still exist.
- GitHub's terms apply separately. Public repositories can be viewed and forked using GitHub functionality under those terms; no-license status is not a promise that nobody may fork, download or retain a copy. Public material is technically copyable and making it private later cannot recall copies already obtained.
- Public source is **source-visible**, not open source under a recognized open-source license. Do not add an MIT badge, SPDX MIT tag, permissive LICENSE template or claim that the entire repository is open source.
- Recruiters can inspect the portfolio. The absence of an explicit license can discourage reuse and makes permission for activities beyond platform viewing/forking less clear. Local-install instructions describe technical capability, not a new general license. If an express, narrowly scoped evaluation/run permission is later desired, approve that separately instead of assuming it is granted here.
- A no-license decision does not eliminate obligations in font/icon/dependency licenses and does not permit public distribution of code owned by another contributor without sufficient permission. No project-wide reservation can override upstream rights in those components.

The README documents this status without claiming that all third-party materials belong to the owner. A custom restrictive license is not added. If contributor ownership or contract terms are uncertain, obtain specific permission or remove/reimplement the affected material before publication. This review is practical release guidance, not a legal determination of ownership.

## Reference points

- [GitHub: licensing a repository](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/licensing-a-repository)
- [GitHub Terms of Service: license grant to other users](https://docs.github.com/en/site-policy/github-terms/github-terms-of-service#5-license-grant-to-other-users)

Review the current platform terms before publication; these links do not constitute a project-code license grant.

## Third-party obligations

`THIRD_PARTY_NOTICES.md` inventories all 296 locked npm dependency entries and 49 restored NuGet packages, including direct/transitive and development dependencies. Available upstream license texts are preserved, and the Vite build emits the notices beside bundled JavaScript/fonts. Optional packages for other platforms are not all installed; their package metadata license is recorded, but their uninstalled package contents were not independently inspected. Most NuGet metadata declares MIT or Apache-2.0; `xunit.abstractions` has legacy license-URL metadata and is a test-only dependency. No dependency packages or compiled backend binaries are committed.

- **Heebo:** preserve SIL OFL 1.1 and copyright; do not sell the font alone or imply ownership. Font files are supplied by npm; no modification/renaming of the font is performed.
- **Lucide:** retain ISC notice for the icon implementation. No Strauss logo assets are included.
- **MIT/BSD/ISC/Apache dependencies:** retain required copyright/license/NOTICE material when distributing their code or binaries.
- **MPL toolchain components:** preserve applicable notices and source-availability obligations if redistributing covered binaries or modifying covered files; this edition does not vendor or modify those package sources.
- **CC-BY toolchain data:** retain upstream attribution/license material if redistributing that data. No such dataset is authored by this project.

The screenshot UI uses these third-party components; their upstream licenses remain unchanged. Existing JavaScript banners are retained, with the additional notices file. Before distributing binaries/containers or changing dependencies, review the exact resulting distribution again; this source-only review is not an exhaustive legal opinion or external compliance certification.
