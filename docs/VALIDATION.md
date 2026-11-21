# Validation of the fictional portfolio edition

**Publication context:** the reviewed portfolio is now [public](https://github.com/hadas1906-commits/StraussDriveAI-Portfolio). The sections below retain dated preparation/test evidence. Earlier statements about an absent remote or an unpublished edition describe their original review stage, not today's repository status. See [release history](RELEASE_REVIEW.md).

These are new runs against `data/portfolio-demo.json`; no private-project test results are reused. The source runtime contains no HTTP AI adapter and registers the deterministic planner unconditionally. No paid provider or cloud service was called. Initial validation: 20 November 2026; final rerun: 21 November 2026 (Asia/Jerusalem).

| Check                            | Command                                                                                  | Actual result                                                                                                           |
| -------------------------------- | ---------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------- |
| Backend build                    | `dotnet build StraussDriveAI-Portfolio.sln --no-restore -m:2`                            | Passed, zero warnings/errors                                                                                            |
| Backend tests                    | `dotnet test StraussDriveAI-Portfolio.sln --no-build --no-restore`                       | 57 passed, zero failed/skipped                                                                                          |
| Frontend tests                   | `npm --prefix frontend test`                                                             | 11 passed                                                                                                               |
| Frontend build                   | `npm --prefix frontend run build`                                                        | Passed; includes third-party notices alongside bundles/fonts                                                            |
| Documented local launcher        | `python3 tools/run_demo.py --api-port 5182 --frontend-port 5183`                         | API healthy, frontend HTTP 200 with demo banner, provider `deterministic-demo`; shutdown verified by both ports closing |
| Independent fictional references | `dotnet run --no-build --project tools/StraussDrive.Data`                                | 50 field checks matched, zero mismatches                                                                                |
| Browser workflows                | `CI=1 PORTFOLIO_API_PORT=5180 PORTFOLIO_UI_PORT=5181 npm --prefix frontend run test:e2e` | 10 passed: five desktop and five mobile Chromium                                                                        |

Local port overrides avoid occupied shared-workspace ports; the documented defaults remain 5080/5173. Windows/macOS launcher paths are not manually verified in this Linux run.

Browser tests use real local ASP.NET/SQLite and Vite servers, with external browser destinations blocked. They cover family comparison and a budget follow-up, reload persistence, eligible waiver and mandatory-vehicle rejection, catalog/comparison rendering, axe WCAG 2 A/AA checks on the comparison page, mobile overflow, private scenario save/reload and explicit employee handoff followed by a representative response/resumed calculation. Four fresh desktop screenshots are captured from this fictional edition; no private screenshots are reused.

Backend tests cover five independent financial fixtures, different occupation groups and eligibility, the original 40/60 family split, electric gross-up, waiver, aliases/IDs, native shaped goal plans and duplicate comparison execution, forbidden authority fields, follow-ups and explicit handoff confirmation, CSRF, ownership, logout revocation and role separation. Retained synthetic security regressions cover the Basic gate and representative session expiry/revocation. A configuration test supplies unusable synthetic external-provider settings and explicitly disables the old deterministic flag; the server still selects the deterministic planner because no networking provider is distributed.

## Independently authored expected displayed results

All figures are fictional demonstration units, displayed with a shekel symbol; year 2099 is not a real tax regime.

| Profile                                 | Current net | Alternative net | Difference |
| --------------------------------------- | ----------: | --------------: | ---------: |
| DEMO-P01 / Alpha: ORBIT → PULSE         |       9,945 |          10,138 |       +193 |
| DEMO-P02 / Beta: MEADOW → HARBOR family |      13,074 |          12,085 |       −989 |
| DEMO-P03 / Gamma: SUMMIT → waiver       |      16,778 |          18,470 |     +1,692 |
| DEMO-P04 / Delta: ORBIT → LANTERN       |      11,152 |          11,203 |        +51 |
| DEMO-P05 / Epsilon: waiver → ORBIT      |      10,591 |           9,555 |     −1,036 |

The reference authoring tool is independent Python Decimal code, not the application engine. Tests read committed expectations and never regenerate them. Beta's family gap is 5,300 − 3,300 = 2,000; employee share 800 and employer share 1,200. The electric benefit is 175 net. Display rounding is separate from precise internal arithmetic.

## Limits

All planning is deterministic or scripted; no live-model accuracy claim is made. Browser tests are emulation in Chromium, not physical devices or other engines. Automated axe checks do not establish complete accessibility. This is not a penetration test, production payroll validation or enterprise readiness review. The .NET test runner emits an informational temporary-artifact-directory message; it does not skip tests.

Review `docs/RELEASE_REVIEW.md` and run `python3 tools/release_audit.py` against the final staged/tracked files before publication. Dependencies require network during initial restore/install; normal demo interactions require no external provider or account. This review does not check remote GitHub/cloud settings because no public repository or deployment is created.

## Recruiter presentation and no-license review

The owner chose public viewing without a project-code license; MIT remains unapplied. Licensing/README/release instructions were updated locally without publication. Four screenshots were visually reviewed. The advisor's clipped/oversized capture and the comparison chart's premature capture were corrected, and an old vehicle placeholder was replaced by the fictional PULSE example. No financial or authentication behavior changed. Frontend build and all five desktop workflow tests passed again for this review; the advisor capture was then checked separately after its framing adjustment. The previous backend/mobile results above are retained evidence, not claimed as newly rerun for this presentation/documentation change.

## Finalization — 21 November 2026

The owner confirmed sole human authorship with AI-tool assistance, including Codex; this declaration is recorded in `LICENSING.md`. The project remains source-visible without a project-code license. Third-party notices are unchanged and the built notices file matches the tracked source byte-for-byte.

Fresh final runs: .NET build passed with zero warnings/errors; 57 backend tests passed with zero failed/skipped; 11 frontend tests passed; frontend build passed; 50 independent financial-reference field checks matched with zero mismatches; all 10 desktop/mobile browser workflows passed. Browser command used `CI=1 PORTFOLIO_API_PORT=5230 PORTFOLIO_UI_PORT=5231 npm --prefix frontend run test:e2e`. No live provider or paid service was called.

All four screenshots were visually reviewed in the recruiter review; the final browser run regenerated them from the same fictional implementation. Advisor framing uses a local multi-vehicle summary; the comparison capture waits for its chart. The README links resolve, the fictional/deterministic labels are accurate, and there is no hosted production link. The 91-file secret/privacy scan passed, including English OCR of four PNGs. The reachable preparation-history blobs were separately checked for common secret patterns and forbidden source artifacts; no matches were found. Original source artifacts had no hash matches in the release file set. No personal profile identifiers, original datasets or application-state files are distributed.

The original worktree remains clean. The separate portfolio has no remote, no project-code LICENSE and no copied private history. Final local changes are committed for review, not published. Technical/source-distribution checks pass; contractual publication clearance remains conditional as explained in `RELEASE_REVIEW.md` because no separate participation/employment/IP agreement was available to review. No requirement for blanket MIT licensing or automatic organizer consent is asserted.

## Published README review — 22 November 2026

This documentation-only revision reorganizes the bilingual README for recruiter review, adds captions/navigation for the four existing screenshots, and distinguishes the published repository from historical pre-release records. The 57/11/50/10 application-test counts above remain the dated 21 November portfolio evidence; no application test suite was rerun for this README change.

The README claims were checked against the domain engine, API role/ownership/CSRF/rate-limit code, deterministic provider registration, goal/capability contracts, Scenario Lab, decision evidence, original fixture, SDK/package configuration, launcher and browser-test configuration. Setup commands were inspected, not represented as new platform tests. PowerShell examples describe syntax only; Windows/macOS runtime support is still unverified, and the browser fixture contains Linux-style temporary paths.

Documentation checks cover Markdown formatting, unique navigation targets, local file links, documentation-only diff scope and unchanged third-party notices/no-project-license status. All four existing public screenshot URLs returned HTTP 200; their PNG data matched the reviewed source byte-for-byte and decoded at 1440×1797, 1440×1200, 1440×2301 and 1440×1266 respectively. Screenshot provenance and visual inspection are retained from the preceding review; images were not altered or regenerated. Browser review is limited to the public GitHub README, not a new application workflow run. No app security, dependency or deployment configuration is changed. The README Mermaid block also rendered successfully with a temporary standalone Mermaid 11 renderer (nine nodes); no renderer dependency was added to the project.
