# WI-022 — Assignment impact previews

Managers and Head Office can review an assignment add or removal at
`/Coverage/Assignments`, linked from their existing landings. The review shows
which Locations change rep, from whom to whom, before the named action saves.
This delivers T-3.1.2 / COV-US-001 using the existing WI-021 ownership rule.

## Review and confirmation

Choose an eligible rep, then an active Region, County, Town or Location to add,
or review removal of one of that rep's existing assignments. Each ownership
transition appears as a plain sentence and an expandable count. The expanded
list contains every affected Location with previous/new owner and source.
Groups appear by descending impact count, with deterministic Location ordering.
Unassigned outcomes explain that the Locations will have no responsible rep.

The review page follows existing Razor server forms, labeled native selects,
antiforgery and redirect after success. A page suits the 140-row review and the
shared convention's allowance for large expandable lists. Native `details`
provide keyboard-accessible disclosure. Add's save button names the territory
and rep; removal says Remove assignment. Cancel navigates away without saving.
The final button supplies explicit confirmation; merely previewing writes nothing.

A valid zero-impact assignment can still be confirmed because it can affect
future Locations. Source-only changes for the same rep do not count as ownership
changes or produce ownership history. Existing duplicate holder validation remains;
transfer rules and the transfer offer belong to WI-024/WI-025.

## API contract

| Operation | Contract |
|---|---|
| `GET /coverage/assignment-options?repSubject=...` | Current eligible team reps, active typed targets and the selected rep's assignments |
| `POST /coverage/assignments/preview` | Existing add fields: RepSubject, Target, optional Reason; returns the impact and proof |
| `POST /coverage/assignments/{id}/remove/preview` | Current Version and optional Reason; returns impact, proof and removal version |
| `POST /coverage/assignments` | Add fields plus PreviewProof and Confirmed=true; returns the created assignment |
| `POST /coverage/assignments/{id}/remove` | Removal fields plus PreviewProof and Confirmed=true; returns the saved changed-Location count |

These production writes now intentionally require preview confirmation. Existing
successful mutation tests obtain that proof first; all prior behavioral assertions
are retained. Existing malformed version/target, eligibility, duplicate, authority,
retirement, history and rollback outcomes continue to be verified.

A preview contains the action, target/rep names, total changed Locations, grouped
sentences and exact Location IDs/names with before/after rep/source details. The
opaque proof binds the actor and exact command, including reason and removal
version, to the reviewed state. It contains no browser-supplied authority.

A `409` for changed reviewed inputs includes a fresh preview in `CoverageError.Preview`.
The BFF replaces the hidden proof/version with those server values, explains
that nothing was saved and requires another action click. It never automatically
submits or retries the refreshed plan. Invalid/foreign proof or absent explicit
confirmation returns `400`. Revoked authority, ineligible staff and invalid or
archived add targets deny the operation; unavailable trusted identities fail closed.

Proofs last 30 minutes and are protected with an API-process-local key. Expiry
returns a fresh preview. API restart or routing confirmation to another API
instance makes the proof invalid and requires a new review. This is a deliberate
fail-closed lifecycle, without a new schema or persisted secret; a deployment that
wants previews to survive those boundaries would need shared protection keys.

## Equality and transaction safety

`AssignmentImpactPreview.Calculate` runs `EffectiveOwnerResolver` over the current
and proposed assignment sets. It performs no I/O and changes no input. Reads,
previews and history-producing saves therefore share Location > Town > County >
Region precedence, including suppressed parents and carve-outs.

The API prepares trusted staff labels/eligibility before SQL, preserving WI-021's
no-network-inside-transaction boundary. Preview and save then read an authoritative
snapshot inside a serializable transaction and check current business-team access.
The fingerprint includes assignments and rowversions, Location paths/names,
geography names/activity/versions, reporting lines/versions, acting role and
prepared trusted staff names/roles/availability. Its conservative database-wide
inputs can trigger an extra refresh after an unrelated geography/team save.
Identity remains a preflight source; no cross-service database transaction is implied.

Save recomputes that fingerprint before mutating. Changed inputs produce a refreshed
preview with no assignment/history writes. If it matches, assignment and immutable
changed-owner history commit in the existing single transaction. Concurrent/repeated
confirmation cannot duplicate an assignment or history. Late-save failures roll
both back; the unchanged rollback assertions verify retry writes exactly once.

## Acceptance verification

| Quoted T-3.1.2 criterion | Evidence |
|---|---|
| “Assigning Wicklow (140 Locations, none assigned) to Colm previews \"140 Locations become Colm's\".” | Exact sentence, 140-row expanded set, no preview writes; confirmed save produces 140 owners/history entries |
| “Assigning Rathdrum to Aoife inside Colm's Wicklow previews \"23 Locations move from Colm to Aoife\".” | Exact sentence/set; Murphy's moves via Rathdrum, Doyle's/Laragh retain Colm via Wicklow |
| “Removing Aoife's Rathdrum assignment previews \"23 Locations move from Aoife to Colm\".” | Exact fallback sentence/set and saved sources; exactly 23 changed-owner entries |
| “Removing Wexford (only Brian) previews \"96 Locations become Unassigned\" and requires an explicit confirm.” | Exact 96-row list; unconfirmed proof rejected without writes; confirmed removal persists 96 Unassigned outcomes across restart |
| “Each count expands to the list of Locations.” | Group totals equal exact unique Location lists; Razor native details/summary and named before/after sources verified for both authorized roles |
| “If the data changed since the preview (another save), saving re-previews instead of applying a stale plan.” | Assignment, added/moved/renamed Location, renamed geography/staff, reporting-line and removal-version changes produce fresh impact/proof without writes; fresh impact requires another explicit confirmation |

The dry-run/property checkpoint passed. The developer explicitly approved completing
WI-022 without intermediate review stops, so work continued through integration.
Eight generated real-SQL add/remove fixtures compare exact changed Location sets,
previous/new reps, winning source targets/names and history with the dry run.

Pre-change characterisation: **83/83**. Final focused integration: **67/67**, including
35 new scenarios and all 32 existing WI-021 mutation cases. Dry-run/proof unit
checks: **2/2**. JavaScript: **34/34**. Solution build: zero warnings/errors.
The complete existing suite passed **1,833/1,833**: API 256, Identity 1,049 and
Web 528, zero failures/skips. The initial Web host crashed natively after 81
passes; those results are excluded. The entire Web suite then passed on an
unchanged rerun with crash capture. Exact commands, successful TRX paths and
publication evidence are recorded in `plan_docs/.agent-notes/WI-022.md`.

## Scope

No migration, new ownership rule/cache, transfer, visit handover, batch/reversal,
specialist or tablet Sync implementation. The full rep territory page remains
WI-023; this item supplies the shared review/confirmation flow it can use.
