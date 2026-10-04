# WI-027 — Location coverage changes

## Rendered-flow checkpoint — 2026-10-04

**Active, not complete.** Approved plan and C1/S1–S19 matrix: plan_docs/.agent-notes/WI-027.md. Both required action sets render and all four actions reach reviewed impact. Pause for the source's **Continue T-3.2.2** before remaining save/history tests, complete regression and delivery.

| Entry from Location page | Reviewed operation | Sample impact | Pre-confirmation writes |
|---|---|---:|---:|
| Walsh's: Assign Laragh (Town) to... | Existing Add preview, trusted Town target | 5 Locations become Colm's | 0 |
| Walsh's: Assign just this shop to... | Existing Add preview, direct Location target | 1 Location becomes Colm's | 0 |
| Murphy's: Change just this shop to... | Existing Add preview, direct exception over Aoife's Town | 1 Location moves to Colm | 0 |
| Murphy's: Transfer Rathdrum (Town, 23 Locations) to... | Existing exact-assignment pull transfer review and preview | 23 Locations move to Colm | 0 |

The real authenticated Razor/BFF/API/SQL scenario `Should_RouteEachLocationActionToExactImpactWithoutSaving_When_ManagerChoosesEligibleRep` verifies all four paths, original-Location cancel links, eligible recipient choices, nonempty existing preview proof and unchanged assignment/history counts. Source transfer selects only the exact Rathdrum assignment; another Town in Aoife's book is excluded.

## Changes and safeguards

- New GET `/coverage/locations/{id}/page/actions` returns a consistent Location snapshot, exact source assignment ID, geographic source footprint and action availability. Existing WI-026 page/history contracts and endpoints remain intact. Reuses EffectiveOwnerResolver and trusted staff labels outside serializable SQL transactions.
- Location page orders Town then shop for Unassigned; shop then source transfer for covered. Sole-shop zero-neighbour text is omitted, singular/plural counts remain accurate. Specialists remain empty; History remains separate.
- LocationChange page selects an eligible rep and optional reason, derives target from fresh server context, and uses existing Add preview/save and shared impact partial. Direct/source assignment changes use the existing Transfer flow. Protected navigation context binds actor, original Location/Town, intended action and observed owner/source; it is distinct from the existing API's required mutation preview proof. Changes to entry scope require a fresh Location review.
- Transfer accepts a validated Location entry, restricts selections to the exact source and its existing children/exclusions, and returns cancel/save to the original known Location route. General transfer/pull navigation retains its existing behavior.
- Existing mutation authority is unchanged: narrower Add over inherited coverage may use an authorized recipient; held direct/source transfer requires management of the source. Source read visibility never grants write authority. Archived Add versus existing archived-source Transfer eligibility remains unchanged.
- No ownership rule, mutation store, history writer, schema/migration, visit/handover or specialist implementation changed.

## Checkpoint verification

- Pre-change characterization: **188/188 Passed**, Completed TRX, total=executed=passed, every result Passed, no failures/skips. Existing ownership/preview/proof/history/transfer/Location behavior verified before application changes.
- Warnings-as-errors solution build: **zero warnings/errors**.
- Corrected affected checkpoint suite: **136/136 Passed**, Completed TRX, total=executed=passed, every result Passed, no failures/skips. **36 new cases**: 14 real BFF/API/SQL cases plus 22 typed action-contract cases through authenticated Razor. Covers all four previews, stale Town/forged entry/wrong Location/unmanaged recipient, source footprint at all four levels, inherited-versus-held foreign authority and malformed context. Existing affected tests still pass.
- Initial focused run: **113 Passed, 1 failed, 0 skipped / 114**, an existing archived-transfer case timed out on its final SQL verification read. No assertion or execution policy changed; the full affected suite rerun above includes that same case and passes. Retain both logs/TRX artifacts.
- Existing WI-026 tests were updated only for intentional WI-027 behavior: executable action links replace disabled future buttons, exact unassigned wording matches CV002-B, and presentation fixtures supply the new separate action contract. Existing role/read/history/encoding/count/no-write assertions remain; disabled-button assertion becomes two executable links plus no disabled buttons.
- Browser checks use actual Razor output captured by real end-to-end tests. Raw samples retained; presentation copies change only known navigation anchors for local sample browsing, without simulating saves. Functional POST routing is proven by the authenticated BFF/API/SQL tests. Desktop and 390px action pages fit without horizontal overflow. Keyboard Tab reaches transfer impact summary; Enter expands it, and the 23-row table wraps within the phone viewport. Visually inspected screenshots.

Local ignored evidence: `.artifacts/wi027/{characterization,checkpoint,checkpoint-corrected,rendered}`; build logs and test logs alongside. Raw samples include both Location pages, four recipient choosers, exact-source transfer review and four previews. Screenshots: unassigned-desktop.png, covered-desktop.png, unassigned-phone.png, covered-phone.png, transfer-preview-phone.png. Temporary server stopped, browser tab closed and viewport reset.

## Acceptance status at checkpoint

1. "Walsh's Shop in Laragh, unassigned with 4 other Laragh Locations, shows ‘Assign Laragh (Town) to...’ first, with ‘4 other Locations in Laragh are unassigned’, and ‘Assign just this shop to...’ beside it." — **Rendered and both paths verified to preview**, screenshot unassigned-desktop.png and two unassigned theory cases.
2. "Murphy's Pharmacy, ‘Aoife (via Rathdrum)’, shows ‘Change just this shop to...’ first, creating a direct Location assignment, and ‘Transfer Rathdrum (Town, 23 Locations) to...’ beside it, continuing to the transfer's impact preview." — **Rendered and both paths verified to preview**; preview proposes direct exception versus transfer of the exact existing Town assignment. Confirmed persistence checks remain after checkpoint.
3. "Every change goes through T-3.1.2's preview and writes history." — **Preview paths proven with zero pre-confirmation writes; confirmed-save/history evidence pending** after Continue T-3.2.2. Existing mutation APIs/history writers are reused.

Remaining agreed work includes direct-assignment change UI path, County/Region selection/exclusions from Location, eligibility/role/reporting/geography/preview staleness and antiforgery variants, confirmed Add/Transfer persistence/history/return routes, retries/rollback, long-label rendering and complete .NET/JavaScript regression. Do not mark WI-027 done or integrate this checkpoint as completed delivery.
