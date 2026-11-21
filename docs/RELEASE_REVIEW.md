# Public portfolio release review — pending owner approval

## Scope and boundaries

This directory is a separate curated edition. The original private repository and its Git metadata are not modified. No source history is copied; a fresh local repository contains only reviewed portfolio files, with no remote. Nothing has been published, pushed or deployed. No production URL, provider key, account credential, paid model adapter or cloud deployment configuration is distributed.

Retained and excluded materials are listed in `PROVENANCE.md`. All five profiles have explicit fictional names and no identity numbers, contact details or account identifiers. Original challenge documents, datasets, answers, screenshots and measured result reports are absent. The new financial expectations were authored using an independent Python Decimal oracle and checked against the retained C# engine.

## Secret/privacy review

`tools/release_audit.py` scans the staged/tracked release file set for common provider tokens, private keys, embedded URL credentials, connection passwords, production/provider destinations, suspicious credential literals, prohibited data/binary files and original challenge identity markers. PNGs receive English OCR if Tesseract is installed. Matches are reported as paths/categories only. Synthetic credentials in authentication tests are explicitly identified as fixtures, not access details for a service.

This is a scoped heuristic scan, not an independent penetration test, legal certification or guarantee against undiscovered secrets. It does not inspect the private repository, remote GitHub settings or service accounts. The fresh history is checked separately to ensure no parent history or remotes. npm packages and third-party notices contain legitimate upstream copyright/attribution names and emails; these are not employee data collected by this demo. Source distributions exclude dependency trees, compiled output, databases, signing keys and test artifacts.

## Preparation results

The final scoped scan reviewed 91 tracked release files, including four PNG screenshots with English OCR. No matching secret/privacy blocker was found; two credential-assignment paths were identified as synthetic test fixtures. There is no Git remote. Build/test/reference/browser results and the verified local launcher are recorded in `VALIDATION.md`. This is a fresh local preparation history only; it uses an explicit placeholder author (`portfolio@example.invalid`) rather than exposing an original private commit email. Before publication, set the owner's chosen public Git identity and optionally re-author the local preparation commit.

## Final clearance status — 21 November 2026

**Technical/source-distribution review: PASS. Overall publication clearance: CONDITIONAL, not yet unconditional. Publication itself remains explicitly unauthorized.**

- The owner confirms sole human authorship with AI-tool assistance, including Codex. No other human contributor permission is identified as outstanding on that declaration.
- No MIT or other project-code license is applied. Source-visible/no-project-license publication is the chosen approach, with GitHub viewing/forking terms and third-party licenses preserved.
- The reviewed distribution contains original fictional examples and fresh screenshots. Supplied challenge/government materials, production URLs, external AI integration, credentials and private history remain excluded.
- No code-publication restriction was identified in the supplied challenge instructions. No separate participation/employment/IP agreement was supplied. **The outstanding contractual condition is confirmation that no applicable separate agreement restricts publication of the retained implementation/artwork, or resolution of such a restriction if one exists.** Sole authorship does not answer that separate contractual question. This is not a finding that organizer permission is inherently required.
- Explicit approval to create/publish the final reviewed portfolio is still required. No public repository, remote, push, visibility change or deployment has been performed.

Third-party notice preservation is sufficient for the reviewed source-only package; no dependency trees, font binaries or compiled application distribution are committed. The local build emits preserved notices alongside its font/JavaScript output. Optional uninstalled platforms and future binary/container redistribution remain outside this source-only clearance.

## Exact publishing steps — execute only after explicit approval

1. Rely on the recorded sole-human-authorship declaration for the contributor question; resolve the remaining separate-contract condition described above. Keep the README no-project-license notice and all third-party notices. Do not add an MIT LICENSE, license badge, or blanket claim over upstream materials. No custom restrictive license is added.
2. Review the finalized local commit and choose the public Git identity before publication. The preparation history currently uses an explicit placeholder identity, not a private personal email. From this directory, inspect `git status`, `git log --all --oneline`, `git remote -v`, `git ls-files`, screenshots, `data/portfolio-demo.json` and `THIRD_PARTY_NOTICES.md`. The local history must contain only this edition; no private-project history or remote may appear.
3. Re-run the commands in `docs/VALIDATION.md`, `git diff --check` and `python3 tools/release_audit.py`. Resolve any findings. Do not stage ignored/generated/private files or use `git add -f`.
4. Request and receive explicit owner approval for publishing the exact reviewed edition without a project-code license. Only after that approval, create **only** `hadas1906-commits/StraussDriveAI-Portfolio` through GitHub, selecting Public, with no generated README/license/gitignore; select no license. Alternatively, run the following **only after approval** from this directory:

   ```sh
   gh repo create hadas1906-commits/StraussDriveAI-Portfolio --public --source=. --remote=origin --push
   ```

5. Verify the public file list and README screenshots; enable secret scanning/push protection where available, set a descriptive About field, and avoid linking the paid production demo. GitHub Pages or a cloud deployment is unnecessary. Recruiters can browse screenshots/code and run locally without access to the author's accounts.
6. Keep `hadas1906-commits/StraussDriveAI` private and unchanged. Do not copy future private commits or data into the portfolio without a fresh review.

These are instructions only, not actions performed by this preparation.
