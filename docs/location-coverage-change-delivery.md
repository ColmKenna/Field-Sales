# WI-027 — Location coverage changes

## Delivery — 2026-10-05

The Location page now leads with Town assignment for an unassigned shop and just-shop change for a covered shop. All four entry actions use the existing reviewed Add or Transfer flow, require explicit confirmation, record actual ownership changes atomically, and return to the original Location.

The user approved the plan and C1/S1–S19 scenarios with **Approved and Apply**, then accepted the rendered-flow checkpoint with **Continue T-3.2.2**. Delivery is the verified feature branch `feature/wi-027-change-shop-s-coverage-from`. Complete regression passes and WI-027 is marked done. Commit and push are the final delivery steps. WI-028 is not started.

## Acceptance criteria — PASS

1. **“Walsh's Shop in Laragh, unassigned with 4 other Laragh Locations, shows ‘Assign Laragh (Town) to...’ first, with ‘4 other Locations in Laragh are unassigned’, and ‘Assign just this shop to...’ beside it.”** Actual authenticated Razor renders this order and wording. Both paths reach real API impact without writes. Confirmed Town Add changes only shops whose effective owner changes, preserving direct overrides; confirmed shop Add changes only Walsh's. Evidence: `Should_RouteEachLocationActionToExactImpactWithoutSaving_When_ManagerChoosesEligibleRep`, `Should_SaveReviewedAddAndCapturedHistory_When_LocationChangeIsConfirmed`, and unassigned desktop/phone screenshots.
2. **“Murphy's Pharmacy, ‘Aoife (via Rathdrum)’, shows ‘Change just this shop to...’ first, creating a direct Location assignment, and ‘Transfer Rathdrum (Town, 23 Locations) to...’ beside it, continuing to the transfer's impact preview.”** Actual Razor renders shop-first and the exact Town footprint. Just-shop save creates a direct exception without moving the Town. Source transfer changes only the selected assignment, preserves its ID and narrower overrides, and leaves the giver's unrelated territory unchanged. Future Locations inherit the transferred territory. Evidence: the four-path preview theory, confirmed Add theory, `Should_TransferExactTownPreservingCarveOutsAndFutureInheritance_When_SourceChangeIsConfirmed`, covered screenshots and the 23-row phone preview.
3. **“Every change goes through T-3.1.2's preview and writes history.”** Confirmed Add, direct Transfer and partial County/Region Transfer use existing preview/save APIs and actor-bound proofs. Missing/altered proof, confirmation or antiforgery saves nothing. Changed snapshots refresh preview and require another confirmation; changed entry scope requires a fresh Location review. Actual owner changes have captured actor/rep labels, reason and direct/territory cause. Excluded and unchanged-owner shops receive no fabricated history. Injected failure after history write rolls back assignment and history together; retry saves once, and replay cannot duplicate history. Evidence: confirmed Add/Transfer, refreshed-proof, bypass, partial-source and rollback/retry scenarios plus existing ownership/history regression tests.

**Source self-verification:** Criteria 1–3 demonstrated — PASS, evidence above. Provisional commit message reconciled with actual diff — PASS: delivery covers Location actions, exact scoped Transfer, eligible recipient selection, preview/confirmation, local return and tests; the card's `feat(wi-027)` format and refs are retained.

## Implementation and authority

- Separate `LocationCoverageActions` contract and GET `/coverage/locations/{id}/page/actions` return one consistent Location snapshot, exact source assignment ID, geographic source footprint and availability. Existing WI-026 page/history contracts remain intact. The reader reuses EffectiveOwnerResolver and acquires trusted staff labels outside serializable SQL locks. The BFF rejects inconsistent IDs, levels, counts, labels and availability metadata.
- Location page renders Town then shop for Unassigned, shop then exact source transfer for inherited coverage, and one shop-change action for direct coverage. Zero-neighbour wording is omitted; counts exclude the current shop and respect overrides. Specialists remain empty and History remains separate; visits/last-call content is absent.
- `LocationChange` derives its target from protected actor-bound navigation context, obtains current eligible reps and optional reason, and reuses existing Add preview/save and shared impact rendering. Direct/source changes use Transfer with only the exact source and its permitted children/exclusions. Cancel/save routes derive from the protected original Location ID. Navigation context never substitutes for the API's mutation preview proof.
- Current authority remains authoritative: managers may create an authorized narrower exception over foreign inherited coverage, while held direct/source Transfer requires authority over the source and recipient. Current team, role, geography and recipient eligibility are rechecked. Existing inactive-source and archived-source Transfer behavior is preserved; new Add requires active geography. General Add/Transfer journeys retain their established behavior.
- Continued inspection found the existing assignment FK to reporting lines: general Head Office Add preview can offer an unconfigured rep whose assignment cannot persist. The new Location Add chooser filters through the existing Head Office reporting-lines read, offering configured active salespeople across teams and rejecting a forged missing-line recipient without writes. Transfer retains its existing prerequisite. This corrects the initial inspection assumption; mutation APIs, permissions, general Add options and schema remain unchanged.
- No ownership rule, mutation store, history writer, schema/migration, visit/handover or specialist implementation changed.

## Approved scenario evidence — PASS

All names and expected outcomes were agreed before implementation. The following maps C1/S1–S19 to actual test families and rendered evidence; parameterized variants exercise the approved conditions.

| Scenario | Evidence |
|---|---|
| C1 Existing behavior | Pre-change characterization 188/188; unchanged ownership, Add/Transfer authority, proof, history and WI-026 read tests in complete regression. |
| S1 Walsh's actions | Four-action preview theory, existing Location Razor page tests, unassigned screenshots: exact order/count/wording. |
| S2 Town Add | Four-action preview plus confirmed Add Town variant: no early write; inheritance, direct override preservation and captured history. |
| S3 Shop Add | Confirmed unassigned-shop Add: one direct assignment, neighbour unchanged, local return/notice/history. |
| S4 Murphy's actions | Four-action preview and covered screenshots: shop-first, Aoife via Rathdrum, exact 23-Location source. |
| S5 Direct exception | Confirmed inherited-shop Add: one changed owner, Town remains Aoife's, direct cause/history. |
| S6 Exact Town Transfer | Confirmed exact-source Transfer: same assignment ID, unrelated giver territory/carve-out unchanged, future-shop inheritance. |
| S7 Existing direct | Same-direct-assignment theory: normal and inactive-giver/archived-geography variants, ID preserved and only one action. |
| S8 Broader sources | Exact-source footprint theory at all four levels; confirmed partial County/Region exclusions; existing rollup/last-child Transfer suites. |
| S9 Held Town edge | Stale held-Town entry rejects without turning into Transfer; changed Location/scope variants save nothing. |
| S10 Recipient eligibility | Forged unmanaged choice; inactive/non-rep/reporting/current-role variants; Head Office configured cross-team choice and missing-line rejection. |
| S11 Source access | Foreign inherited/direct authority theory, existing WI-026 all-shop read boundaries and existing Add/Transfer team policy tests. |
| S12 Zero/singular/four | Sole-unassigned-with-direct-neighbours scenario verifies Town-first, omitted zero text and only actual owner change; existing count Razor cases. |
| S13 Staleness | Eight label/assignment snapshot-refresh variants for Add/Transfer plus moved-shop/direct-created/source-owner/source-team/receiving-team variants. Refreshed proof clears confirmation and cannot save unconfirmed. |
| S14 Bypass/authentication | Twelve missing/tampered proof, confirmation, command and antiforgery variants; action endpoint role/scope/subject/current-role failure cases; existing actor-bound proof tests. |
| S15 Protected context | Four expanded Transfer context variants plus forged entry/wrong Location; malicious return URL cannot replace the trusted Location route. Existing general Transfer/Pull tests retain their navigation. |
| S16 Safe failure | Twenty-two typed-contract cases; no eligible reps, archive guidance, missing Location, unavailable staff/current roles; existing trusted-label/API failure cases. |
| S17 History/atomicity | Confirmed Add/Transfer count/captured-label/cause/reason assertions, preserved override history, replay rejection and two injected rollback/retry cases; existing same-owner/source-only history tests. |
| S18 Navigation/return | All four real previews have unchanged assignment/history counts and original-Location cancel links. Confirmed Add/direct/source saves redirect locally and show refreshed owner/history; Razor fixture performs no save. |
| S19 Rendering/accessibility | Four authenticated short/long/markup/unassigned Razor variants, encoded labels/reason and server-only tokens; desktop/390px action, chooser and preview browser checks, keyboard selector/expanded table and no overflow. |

## Verification

- Pre-change characterization: **188/188 Passed**. Checkpoint affected suite: **136/136 Passed**, including 36 new cases.
- Final warnings-as-errors solution build: **0 warnings, 0 errors**.
- Final affected suite: **197/197 Passed**, all results Passed, Completed TRX, total=executed=passed, no failures/skips. Includes **97 new cases**: 71 real authenticated BFF/API/SQL, 22 typed-contract and 4 authenticated Razor/render cases.
- Complete solution regression: **2,125/2,125 Passed** — API **268**, Web **808**, Identity administration **1,049**. Every project TRX is Completed; total=executed=passed and every individual result is Passed, with no failures/errors/timeouts/nonexecuted cases or skips. All projects ran after final application/test changes with `--no-build --no-restore -m:1 --settings sqlserver.runsettings`, without a test filter.
- Existing JavaScript suites: **34/34 Passed**, no failures/cancellations/skips/todo.
- No assertion or execution policy was weakened, no tests skipped/deleted, and no test filter is used for complete regression. Existing WI-026 expectation changes reflect the approved enabled actions/exact wording and separate presentation contract, preserving read/role/history/count/encoding/no-write assertions.

Browser evidence uses actual captured Razor markup and final CSS. Local samples are static presentation evidence; authenticated BFF/API/SQL scenarios prove functional POST routing, saves and failures. The original two action sets and 23-row transfer preview fit at 390px. Continued long-label checks exposed chooser/preview overflow; wrapping scoped to Location-origin pages fixes it. Final phone document width is 375px within 390px, desktop 1265px within 1280px. Tab reaches the recipient selector and impact summary; Enter expands the impact table without overflow. Screenshots visually inspected. Temporary server stopped, browser tab closed, viewport reset.

Ignored local evidence: `.artifacts/wi027/{characterization,checkpoint,checkpoint-corrected,final-focused-corrected,regression,rendered,rendered-final}`, build/test logs and `javascript-final.log`. Original checkpoint screenshots: unassigned-desktop.png, covered-desktop.png, unassigned-phone.png, covered-phone.png, transfer-preview-phone.png. Continued screenshots: long-choose-phone.png, long-choose-controls-phone.png, long-preview-phone.png, long-preview-desktop.png.

Diagnostic history is retained in agent notes: initial checkpoint SQL timeout passed on complete affected rerun; new test expectations were corrected to match existing validation/refresh behavior and the reporting-line FK. Final verified results above supersede diagnostics. No outstanding scenario failure or product decision remains.
