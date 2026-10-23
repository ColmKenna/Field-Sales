# WI-031 — Say what removing a permission will do before saving

Both permission views now use **Remove → Review → Confirm removal / Cancel**. The review names the
affected rep or selected cohort, explains when access changes, and says existing unsent lines will
still send. Review and Cancel write nothing. Confirmation uses the reviewed selection and reason;
current authority and expected last-change ids are checked again by the existing API rule/writer.

Source: T-3.4.3, COV-US-009 CV009-E/F, M-14/M14.2. The developer approved the plan and 17 Human-Led
scenarios, then accepted the single-rep and cohort checkpoints (`0ded5cc`, `948e68f`) before final
verification on 20 October 2026.

## Acceptance evidence

The developer resolved MI-44 for this message: **keep the rep count, omit the order-line count, and
say existing unsent lines will still send**. They chose a review step with **Confirm removal** and
**Cancel**. This supersedes the numeric order-line portions of the quoted source criteria; the
console's source card/story text remains intact. The accepted single-rep preview uses neutral
“their next Sync” because the directory has no pronoun data.

| Quoted source criterion | Approved behavior and evidence |
|---|---|
| “Removing Colm's Pharmacy-only medicines first says \"Colm will lose Pharmacy-only medicines at his next Sync. 2 lines already on In Progress orders will still send.\" (CV009-E)” | **PASS under the approved amendment.** The review says **“Colm will lose Pharmacy-only medicines at their next Sync. Any lines already on unsent orders will still send.”** `PermissionRemovalReviewPageTests.Should_ShowConsequenceWithoutSaving_When_ReviewingASingleRemoval` posts the real Razor removal form through the BFF/API and verifies this text, the reason, confirmation controls and unchanged SQL state/history. Cancel also leaves the permission and record unchanged; confirmation records one removal. |
| “Removing it from Colm, Aoife and Niamh says once how many reps lose it at their next Sync and how many In Progress lines will still send (CV009-F)” | **PASS under the approved amendment.** The shared review renders one consequence paragraph using the protected selection's rep count, followed by the selected names. The count-based formatter produces **“3 reps will lose Pharmacy-only medicines at their next Sync. Any lines already on their unsent orders will still send.”** for three selected reps. `PermissionRemovalCohortPageTests.Should_ShowOneConsequenceWithRepCount_When_ReviewingACohort` verifies one-/two-rep real-form reviews, the exact single message, selected names/reason and unchanged SQL state/history; the unit count/plural cases verify the same formatter. The actor must have authority over every selected rep: Head Office can act across teams, and nobody can remove their own permission. |

Archived groups use accurate wording: the permission will be removed, the group already grants no
access, and restoring the group will not restore the removed permission. They do not claim a new
loss of access. Inactive/former holders remain removable. The group chooser includes archived
groups only while they retain holders; the default active-only API chooser still serves grant clients.

## Confirmation and transaction behavior

- [PermissionRemovalReviews](../src/FieldSales.Web/Coverage/PermissionRemovalReviews.cs) uses the
  existing persisted Data Protection key ring and a 30-minute lifetime. The protected review binds
  actor, group, trusted labels, selected reps, expected last-change ids, reason and single/cohort
  context. Missing, altered, expired, foreign-actor or wrong-context reviews cannot confirm.
- The [shared partial](../src/FieldSales.Web/Pages/Coverage/_PermissionRemovalReview.cshtml) displays
  the consequence and the reviewed reason. Extra posted selections or reasons cannot replace that
  command. A stale permission requires a fresh review; replay cannot duplicate records.
- Both API routes use the existing `RestrictionPermissionRule` and `RestrictionPermissionWriter`.
  Website confirmations recheck current roles/team membership. Direct authenticated business API
  calls retain those authorization and current-state checks.
- Cohort grants/removals share the existing serializable transaction orchestration. Selected
  reporting lines stay locked through the catalogue commit. Removals are all-or-nothing; every
  selected rep receives a separate existing append-only record with one actor/date/shared reason.
- The BFF rejects malformed or contradictory group/removal replies: wrong group/rep set/count,
  unexpected change/reason, duplicate ids/sequences/reps, inconsistent actor/time, missing values
  or outage. A POST is never retried automatically.

## Approved scenario map

Integration classes are under `tests/Integration/FieldSales.Web`; review/client unit classes are
under `tests/Unit/FieldSales.Web`. The original grant and removal assertions remain; the original
single-rep page test now follows review then confirm and checks no write during review.

| Approved scenarios | Evidence |
|---|---|
| 1–3, single review/Cancel/confirm | `PermissionRemovalReviewPageTests`: real forms, exact consequence/reason, no review/Cancel write, one removal and identical recorded actor/date/reason. |
| 4, protected confirmation | Single/cohort page theories plus `PermissionRemovalReviewTests`: missing/altered/expired/foreign actor, mismatched rep/group, missing confirmation and cross-flow proof rejection. |
| 5, fresh review after change | Both page classes remove/regrant externally before confirmation. The stale review cannot write; a fresh review then succeeds. |
| 6, authority after review | Both page classes change the actor's roles or a selected rep's team after review. Confirmation is denied and neither permission table changes. |
| 7, one cohort consequence/count | One-/two-rep real-form review cases, selected-name/reason checks, and unit count/plural/archived cases. |
| 8, selected/unselected and shared record | `PermissionRemovalCohortTests` and the cohort page confirmation test remove Aoife/Colm, keep Ciara, compare both views and reject replay. Extra posted fields cannot change the reviewed command. |
| 9, invalid/stale/not-held selection | API/form theories: empty, duplicate, malformed, stale or not-held selections cannot partially save; a later stale row rolls an earlier removal back. |
| 10, outside team/self | API and form denial cases cover mixed outside-team selection, Sales Manager self-change and Head Office self-change. No partial write. |
| 11, persistence failure | `RestrictionPermissionCohortSafetyTests.Should_RollBackAllRemovals_When_APersistenceFailureOccursPartWay`: after one/two real saved removals, both tables are observed inside the transaction; a fresh scope proves complete rollback, and retry succeeds once. |
| 12, archived/inactive holders | Single/cohort page and API cases cover archived, inactive, former-rep and combined states, truthful archived consequence and chooser reachability. |
| 13, identity/request boundaries | Identity outages at review/confirm, unknown staff, missing antiforgery, API role/scope and anonymous request denial; neither table changes. |
| 14, reason rules | Unit/API/form theories cover absent, blank, trimmed, 1000-character, 1001-character and control-character reasons. Confirmation uses the reviewed normalized reason. |
| 15, inconsistent replies | `RestrictionPermissionCohortClientTests.Should_FailClosed_When_TheRemovalApiReplyIsInconsistent` and group-page flag cases; valid replies stay accepted and no POST is retried. |
| 16, reporting-line race | `RestrictionPermissionCohortSafetyTests.Should_KeepReportingLinesLocked_When_RemovalRacesWithATeamChange`: a pre-save team change denies all; SQL DMV evidence shows an in-flight team change waits until the whole cohort commits. |
| 17, grants preserved | Existing single-rep and group grant rule, persistence, API, Razor and client tests remain in the permission regression set and complete suite. |

## Verification

- Characterization before changes: 118 existing permission tests passed.
- Accepted checkpoints: 196 website unit / 64 permission integration tests for single-rep review;
  then 228 website unit / 127 permission integration / 22 API permission tests for the cohort.
- Rendered normal/archived/completed/stale single-rep states at desktop 1280 px and phone 390 px;
  cohort selection, one-rep review, multi-rep review, archived review, completed removal and stale
  review at both widths. All 20 layouts have no document overflow or empty error alerts. Phone
  tables scroll within keyboard-focusable regions to reach the selection controls.
- Final no-incremental solution build passed with **0 errors** and only the 79 reported,
  pre-existing NU1902/NU1903 audit exceptions; no compiler/analyzer warning or package change.
- All **34 JavaScript tests passed**: admin UI 18, product form 5, delivery console 11. Admin tests
  ran in an isolated copy with lifecycle scripts disabled; all 15 source/test/package files matched
  the repository originals byte-for-byte.
- **2,421 distinct .NET tests passed**, using the latest complete results for all seven assemblies:
  API integration 110, DemoData integration 3, Identity Admin integration 794, Web integration 848,
  API unit 180, Identity Admin unit 258, Web unit 228. Every individual result was verified Passed;
  none was skipped or unexecuted.
- The initial full run had one existing Identity Admin test fail while deleting its temporary
  database (`TemplateAdoptionBaselineTests.Should_SeedTheStaffClientAndApi_When_AFreshDevelopmentHostStarts`,
  SQL execution timeout in `FreshDatabases.DeleteDatabaseAsync`). All six other projects passed.
  The **entire unchanged 794-test Identity Admin project** was rerun with the same settings and
  passed. The cleanup timeout did not recur; no source, assertion or timeout change was needed.
  The original run and full-project rerun are both retained in `final-dotnet/` and
  `final-identity-retry/` under the artifact directory.
- WI-031 is marked done in the console after green verification. The feature branch is committed
  and pushed at the developer's integration boundary.
- Console data compared with main base `7f75ca9`: only the WI-031 status entry differs. The browser
  renders its done chip with no page errors (`console-wi031-done.png`). `git diff --check` passes.

Local logs, TRXs and screenshots are under `.artifacts/wi031` and are not committed. The complete
.NET command uses `-m:1 --settings sqlserver.runsettings` to bound SQL Testcontainers concurrency.

## Scope and hand-offs

No permission policy, record schema, migration, identity administration or order telemetry changed.
**WI-043** owns actual tablet Sync visibility and the existing order-line behavior; WI-031 delivers
the approved website consequence and removal confirmation. No tablet outcome is claimed here.

All placeholders and decisions required for WI-031 are resolved. PR creation, merge, rebase,
force-push and branch deletion remain developer-controlled integration work.
