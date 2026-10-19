# WI-030 — Grant a permission to a cohort from the group's page

A Sales Manager or Head Office User can open **Coverage → Rep permissions → Group permissions**,
choose a Restriction Group, select eligible reps and grant with one optional reason. Each rep gets a
separate entry in the existing append-only permission record. The per-rep and per-group pages show
the same state and last change. The grant also starts from the group's reference-list entry.

Source: T-3.4.2, COV-US-009 CV009-A–D, M-14/M14.1. WI-029's rule and writer remain authoritative.

## Acceptance evidence

| Criterion | Evidence |
|---|---|
| "The group's page lists reps with Granted / Not granted and each one's last change (CV009-A)" | **PASS.** `Should_ShowStatesAndLastChanges_When_OpeningTheGroupPage` reads the real API and SQL: current grant, removed permission, captured last changes and no-change rows, in rep-name order. `Should_LockOutsideTeamRows_When_TheViewerIsOnlyASalesManager` renders these rows through the BFF. |
| "Selecting five reps and granting with reason 'Completed training 15 Sep 2026' creates a separate record each (CV009-B)" | **PASS.** `Should_RecordFiveSeparateGrants_When_GrantingToFiveSelectedReps` selects Colm, Aoife, Ciara, Brian and Niamh as Head Office user Orla. There are five different record ids and five current grants; every entry captures 15 Sep 2026 14:02:30 UTC, Orla Kenny and the training reason. The real website form independently performs the same five-rep action in the page test below. |
| "A grant from the group's page appears on the rep's own permissions page (CV009-C)" | **PASS.** `Should_ShowTheSameRecord_When_OpeningEachGrantedRepsPage` submits the real website form, follows its redirect, and opens all five per-rep pages. Both views show the recorded grant and the same actor, timestamp and reason. The API test also compares the complete returned last-change records for equality. Reading either view adds no records. |
| "A rep outside my team is listed but can't be selected (CV009-D)" | **PASS.** Niamh's page offers Colm/Aoife checkboxes and shows Brian/Ciara/Sorcha as View only. No checkbox is rendered for a locked row. Forging an Aoife-plus-Ciara selection through either the BFF or API returns 403 and leaves both permission tables empty. The original per-rep denial tests also remain in the verification scope. |

## Authorization and transaction behavior

- Every selected rep uses `RestrictionPermissionRule.CanChange`: the rep's own manager or Head
  Office may grant; nobody can grant to themselves. A selling manager is eligible for another
  authorized actor to grant. A Head Office actor is used for the five named reps to preserve this rule.
- The page's selectable flags are context only. The API checks current roles and every reporting
  line again. A stale page cannot grant after its rep moves teams or its actor loses a role.
- Trusted staff lookups complete before SQL transactions. Selected reporting lines stay locked in
  a serializable DirectoryDb transaction through the serializable CatalogueDb transaction's commit.
  Subjects are processed in deterministic order. All grant records use one timestamp and shared reason.
- `RestrictionPermissionWriter.ApplyAsync` writes each existing record/current grant within that
  single catalogue transaction. It commits only after all selected grants succeed. A failed write,
  stale row, already-held permission, invalid rep or forbidden selection cannot produce a partial save.
- Each selection carries the expected last-change id. Replaying a successful form returns conflict
  without duplicate records. Invalid responses are refused by the BFF, and POSTs are never retried.
- The optional reason follows WI-029's trimming, 1000-character and control-character rules.
- Already-granted rows are locked. Inactive current holders stay visible without grant controls.
  The chooser lists active groups; direct archived group pages are read-only. Identity outages fail closed.

## Approved scenario map

The developer approved all 15 scenarios before test code, then accepted the review checkpoint with
`Continue T-3.4.2` on 19 October 2026. No scenario was dropped or deferred to another item.

| Approved scenarios | Location and evidence |
|---|---|
| 1, group states/last changes | `RestrictionPermissionCohortTests.Should_ShowStatesAndLastChanges_When_OpeningTheGroupPage`. |
| 2, five grants | `RestrictionPermissionCohortTests.Should_RecordFiveSeparateGrants_When_GrantingToFiveSelectedReps`. |
| 3, both views share the record | The five-rep API test and `RestrictionPermissionCohortPageTests.Should_ShowTheSameRecord_When_OpeningEachGrantedRepsPage`. |
| 4–5, locked outside-team rows and forged mixed selection | `RestrictionPermissionCohortPageTests.Should_LockOutsideTeamRows_When_TheViewerIsOnlyASalesManager` and `RestrictionPermissionCohortTests.Should_RecordNothing_When_AForgedSelectionIncludesAnOutsideTeamRep`. |
| 6, Head Office and self-change | `RestrictionPermissionCohortTests.Should_AllowCrossTeamGrantsButRejectSelfChange_When_ActingAsHeadOffice`. |
| 7, empty/duplicate/malformed selections | API theory (6 cases) plus real-form empty selection and malformed expected-change id. |
| 8, stale/already-held permissions | API conflict test proves a later stale row rolls back an earlier inserted grant; the page test replays the original five-rep form. |
| 9, injected failure part-way | `RestrictionPermissionCohortSafetyTests.Should_SaveNoPartialGrants_When_AWriteFailsPartWay` (2 cases). After one/two real saves, the interceptor reads matching history/current rows inside the transaction and throws; a fresh request sees neither table changed, and a retry grants all three exactly once. |
| 10, current authority and locking | `RestrictionPermissionCohortSafetyTests.Should_UseCurrentAuthority_When_TeamOrActorRolesChange`. A pre-save team change rejects the entire action; an in-flight team edit demonstrably waits in `sys.dm_exec_requests` until the cohort commit; the next request uses the new team/current roles. |
| 11, identity and rep eligibility | API test: unavailable identity, unknown subject, inactive rep and non-rep; neither table changes, and identity is never consulted inside a transaction. |
| 12, archived/missing groups | API and page tests cover read-only archive state, inactive held rows, active chooser, a stale grant form and missing group. |
| 13, shared reason | API theory (6 cases): absent, blank, trimmed, 1000 characters, 1001 characters and control character. |
| 14, request boundaries | Real page antiforgery check; Field Salesperson website denial; API role/scope denial. |
| 15, untrusted API replies | `RestrictionPermissionCohortClientTests` (51 unit cases): group pages/chooser and cohort responses; wrong group/rep, missing/duplicate records, contradictory state, invalid identifiers/actor/time/reason, null/malformed payloads and outage. Valid replies remain accepted; no POST retry. |

Integration tests live under `tests/Integration/FieldSales.Web`; client tests under
`tests/Unit/FieldSales.Web`. Existing fixtures and assertions remain intact; the permission test host
accepts optional EF interceptors for controlled failure injection.

## Verification

- Baseline before behavior changes: 40 existing permission tests passed (persistence 10, rule 12,
  website/API/hand-offs 18).
- Checkpoint: 42 permission integration tests and 38 page/reference-list tests passed (overlapping
  runs). Desktop 1280 px and phone 390 px previews were reviewed for manager, Head Office,
  five-grant result, archived, empty and stale-save pages. No page overflow or empty error banner;
  the permissions table scrolls inside a keyboard-focusable region.
- Remaining focused scenarios: 3 SQL safety cases and 51 BFF client cases passed.
- Final build: `dotnet build FieldSales.slnx --no-incremental -m:1 -warnaserror
  '-warnnotaserror:NU1902;NU1903'` passed with 0 errors and only the pre-existing audit codes;
  no compiler/analyzer warnings. No package changes.
- JavaScript: **34/34 passed** (admin UI 18, product form 5, delivery console 11). The admin tests ran
  in an isolated artifact copy with lifecycle scripts disabled, leaving vendored sources untouched.
- Complete .NET suite: **2,295/2,295 passed** using `dotnet test FieldSales.slnx --no-build
  --no-restore -m:1 --settings sqlserver.runsettings`. API integration 110, DemoData integration 3,
  Identity Admin integration 794, Web integration 766, API unit 180, Identity Admin unit 258,
  Web unit 184. All seven TRX runs Completed; every individual result was checked as Passed;
  none failed, skipped or remained unexecuted.
- `git diff --check` passed. WI-030 is marked done in the console; branch
  `feature/wi-030-grant-permission-cohort-from-group` is committed and pushed for developer integration.

TRX, logs and preview images are under `.artifacts/wi030` and are not committed.

## Scope and hand-offs

- No schema change or second permission rule/record. API endpoints are owned by Field Sales API,
  behind the existing server-token BFF for website calls.
- **WI-031** owns permission removal controls/consequence messaging, including the cohort removal
  statement and MI-44. It must reuse this authorization and atomic transaction behavior.
- **WI-043** owns tablet Sync visibility and existing order lines. This item does not claim tablet delivery.
- No new identity administration, full-history page, automatic integration or alternative front end.
- The provisional source commit was reconciled with the scoped implementation. Commits use the
  card's `feat(wi-030)` format and `Refs: WI-030, T-3.4.2, COV-US-009`.

All required placeholders and behavior decisions are resolved in the approved plan and standing
decisions; there is no outstanding missing information for WI-030.
