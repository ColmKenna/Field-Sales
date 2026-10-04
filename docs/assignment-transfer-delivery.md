# WI-024 — Assignment transfers

Managers and Head Office can open Transfer beside a rep's assignments filter, review the selected areas, exclude rows, choose a receiving rep and explicitly confirm the impact. The operation moves assignments, preserving their IDs; future shops follow the transferred geography. A partial County selection creates Town carve-outs. Taking the last inherited Towns or Counties moves their parent assignment and displays the corresponding roll-up notice.

## Implementation and boundaries

`AssignmentTransferPlanner` is a pure calculation over the complete geography and current assignment set. Empty and archived units count when deciding roll-ups. Narrower assignments stay with their owners unless separately selected. Parent selections subsume inherited child selections; exclusions are converted into the remaining child scopes. New carve-out IDs are deterministic for the source assignment and child scope. No redundant child rows are created when their parent moves.

`AssignmentTransferStore` supplies scoped review, preview and save endpoints. Receiving reps must be active Field Salespeople with reporting-line records. Managers must currently manage both giving and receiving reps; Head Office can transfer across teams. A trusted inactive giving rep can transfer out existing coverage. Archive flags remain unchanged.

Preview uses the existing effective-owner resolver and impact calculator. The shared expiring proof binds actor, source, recipient, normalized selection, reason and the authoritative snapshot. Changed data refreshes the preview or denies the operation. A refresh saves nothing and requires a new confirmation. The BFF retains the reviewed command across refreshes, clears the old confirmation and keeps tokens server-held.

Assignment changes and append-only history commit in one serializable DirectoryDb transaction. Trusted staff data is obtained before SQL locks. Each changed effective owner gets one history entry, with the old/new rep and source, actor, reason, timestamp and shared operation ID. Explicit direct Location transfers use DirectLocationAssignment; territory changes use TerritoryAssignment. No history is written for preserved owners.

The existing Razor Pages BFF provides the review, recipient and confirmation steps. County children and Region Counties are selectable. Foreign or separately assigned child scopes are labelled and preserved. The small selection script synchronizes the parent and child checkboxes; the server independently validates scopes and applies exclusions. Cancellation returns to the known giving-rep territory page. County-name filtering also finds an inherited Region assignment.

Receiving-rep pull, Take over, leaving-rep questions, visit handover, temporary batches and specialist permissions belong to later items. This change adds no SQL migration, visit operation, identity-admin UI or alternative frontend.

## Acceptance evidence

1. **“On Colm's page, filtering his assignments and choosing \"Transfer...\" opens a review with every filtered row selected; rows can be removed before choosing the receiving rep.”** U1 tests cover County/Town filtering, initially selected rows, exclusion before recipient choice, both staff roles, CSRF and stale-confirmation behavior. Region County-name filtering is checked through the real BFF. Browser checks exercise checkbox exclusion and keyboard Space.
2. **“Transferring the whole Wicklow County to Niamh moves the County assignment; Locations show \"Niamh (via Wicklow)\"; carve-outs keep their owners.”** P1 and I1 assert the unchanged County ID, 117 changed inherited Locations, 23 preserved Rathdrum Locations and preview/saved source equality.
3. **“Transferring only Bray, Greystones and Wicklow Town creates a Town assignment to Niamh for each; the County assignment stays with Colm.”** P2/I1 assert exactly those three new Town rows and preserved County/carve-out rows.
4. **“After criterion 3, transferring every remaining Town Colm holds through Wicklow to Ciara moves the County assignment to Ciara too, and the preview says \"Wicklow (County) moves to Ciara with its last Towns.\"”** P3/I1 assert the exact notice, retained earlier carve-outs, no redundant Ciara Town rows, and a subsequently created Town/Location inheriting Ciara through the same County ID.
5. **“Transferring Wexford, the last County Colm holds through South East, moves the Region too: \"South East (Region) moves to Ciara with its last Counties.\"”** P4/I1 assert the exact Region notice and assignment set after partial Wicklow and last Wexford transfers.
6. **“Every Location whose owner changes gets a history entry.”** I1 compares exact preview Location IDs, old/new reps and sources with saved ownership/history, including mixed direct/territory causes. I2 injects failure after history save and asserts complete assignment/history rollback. I3/I4 prove rejected or stale operations do not save.

## Verification record

- Existing coverage characterization before shared changes: 64/64 passed.
- Final planner/shared preview-proof checks: 14/14 passed, Completed TRX.
- Typed client and authenticated Razor pages: 17/17 passed, Completed TRX.
- Existing JavaScript suite: 34/34 passed; isolated identity test files match repository bytes.
- Final warnings-as-errors build passed with zero warnings/errors. Final code head `2719248fe2e56a40ecd3c9df47cd6e08e51336b8` passed the complete real-SQL CI suite: **1,921/1,921 .NET tests** (API 268, Web 604, Identity 1,049), zero failed/skipped. All **61 new cases** passed (12 planner, 49 transfer/client/page).
- Browser: desktop and 390px viewport, native selection/disclosures, keyboard Space and readable roll-up/confirmation layout. At 390px, document width is 375px without horizontal overflow. Images and authenticated HTML are under `.artifacts/wi024/rendered/`. The local render uses a contract fixture; SQL-backed behavior is verified separately through integration tests.
- Initial local SQL run was unavailable at fixture startup because the Mac disk was nearly full and Docker reported I/O errors. No test was skipped or softened. Full real-SQL verification uses the unchanged GitHub CI workflow.
- Initial CI run 37183243434: API 268 and Identity 1,049 passed; Web 598 passed / 3 failed. Corrected the new recipient fixture's missing reporting-line FK and used the existing TestHost exception convention for injected failure, retaining rollback assertions. The store now validates a receiving reporting line before preview/write. Corrected CI run **37199164668**, job **111427095301**, passed. Its decoded logs confirm the previously failing cases, explicit expiry/missing-reporting-line cases, both direct-assignment variants and all existing tests passed.

Final PR head `7653549a77144ed5c4fe09417a57ea48e4876151` passed CI run **37199665750**, job **111428565036**, with **1,921/1,921** tests and zero build warnings/errors. The user freed disk space and Docker is responsive again.

PR: https://github.com/ColmKenna/Field-Sales/pull/5. Agent handover: `plan_docs/.agent-notes/WI-024.md`. All six acceptance criteria pass. WI-024 is marked done. PR #5 merged on 2026-10-04 at `084f8fe9efda978b8f418efb3da5165668bdd48a`; local main is synchronized with the merge. Next item is WI-025, receiving-rep pull; it has not been started.
