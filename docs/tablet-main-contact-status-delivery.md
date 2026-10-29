# WI-036 — Main Contact status on the tablet

T-2.5.2 / DIR-US-004 S5, on `feature/wi-036-show-inactive-main-contact-tablet`
from `main` at `81ee071`. The developer approved the plan and Apply on
29 October 2026. This item is Agent-Autonomous, with agent-owned tests and no
mid-run checkpoint. WI-020 and WI-035 are complete predecessors.

## Delivered behavior

After Sync, a rep opening a flagged shop sees exactly:

> Main contact: Mary Walsh (inactive) — replacement needed

This line is in the shop's identity block, as T5.2 requires. An active Main is
shown as `Main contact: <name>`, with its existing type, phone and email details.
Ordinary inactive linked contacts do not create replacement warnings.

The server uses WI-020's stored `RetiredMainContactId` as the gap's authority.
Reactivating the outgoing contact does not fill the gap or select that person as
Main. The tablet then uses the existing neutral wording, `Main contact replacement
needed`, with the outgoing name and no inactive marker. Explicitly selecting a
replacement clears only that Location's gap on the next Sync.

The status is downloaded data. Reading the Location needs no online contact
lookup, and the state survives reopen. WI-035's retained shop identities also
keep this status while Unsent work references them; reassigned shops retain the
existing notice and offer no new-work actions.

## Contract and storage

- Snapshot **1.2** adds one optional Location property, `mainContactReplacement`,
  carrying the outgoing contact's ID, name and inactive state. The current major,
  payload families, counts and endpoints are unchanged.
- The original `MainContact` property remains active-only. A retired outgoing
  person is never put in that field for older readers. The added property is
  omitted when there is no gap.
- A frozen older Location reader keeps all original fields and ignores the new
  property. The new app still reads 1.0 and 1.1 snapshots; an absent property does
  not imply a gap. The explicit historical fixtures remain intact.
- Tablet storage adds no SQLite table, column or migration. Schema version stays
  **2**. Per-Location replacement data is private JSON metadata on the existing
  generation row, under `locationMainContactReplacements`; it is not added to the
  wire manifest.
- The metadata is written to the staged generation only after hash/count checks.
  The existing atomic pointer switch publishes it with that generation's rows.
  Retiring and deleting a generation also deletes its metadata. A fresh generation
  without a gap cannot accidentally inherit one from an earlier snapshot.
- Contact and private metadata serializers use explicit source-generated type
  information. The whole import/read/render path also passed a separate smoke
  run with reflection-based JSON serialization disabled.

No Directory mutation rule, identity configuration, BFF behavior or package
version changes. Contact editing, replacement commands, online ordering setup,
gap lists and other later tablet functionality remain outside this item. WI-035's
approved temporary history/action behavior is preserved.

## Acceptance criteria

1. **“With Mary Walsh Inactive and Rathdrum flagged, after sync the rep sees
   \"Main contact: Mary Walsh (inactive) — replacement needed\".”** The protected
   snapshot API test retires Mary through the real Directory endpoint and checks
   both shops where she was Main. Real SQLite imports preserve the state, and the
   actual-source component test verifies the exact line inside `.shop-identity`.
   The rendered inactive state was visually inspected against T-05/T5.2.
2. **“With an active Main, the rep sees \"Main contact: <name>\".”** The actual
   Location component explicitly prefixes the active name. Existing type/phone/
   email and equal primary actions remain covered. API and component tests verify
   the transition to a selected replacement and removal of the old warning.
3. **“The status comes from the snapshot and is correct offline.”** Core tests
   reopen real SQLite stores with online reads configured to fail. They verify
   inactive/reactivated/replaced generations, older versions and retained shops
   after four more refreshes and cleanup. Failed or cancelled downloads preserve
   the old contact state, active generation and Unsent fingerprints.

## Verification

Final per-project verification is **2,664 passed, 0 failed, 0 skipped** across
all 12 projects. This is the latest complete result for each project, not a claim
that the first full run was green: the unchanged API project was confirmed after
its startup-lock failure, and Core was confirmed after the final input guard.
The other 10 project results are from the initial complete run.

| Project | Passed |
|---|---:|
| API integration | 143 |
| Demo data integration | 4 |
| Identity integration | 804 |
| Snapshot spike integration | 22 |
| Tablet component integration | 11 |
| Tablet Core integration | 103 |
| Tablet spike integration | 41 |
| Website integration | 873 |
| API unit | 182 |
| Identity unit | 256 |
| Tablet Core unit | 6 |
| Website unit | 219 |

The complete run used `dotnet test FieldSales.slnx -c Release --no-build -m:1
--settings sqlserver.runsettings --blame-hang-timeout 2m --logger trx`. The API
confirmation used the same settings with the complete API project and no filter.
Core confirmation ran the complete Core project with the same run settings.
No existing test was weakened, skipped or deleted. The 1.2 version assertions were
updated intentionally; historical 1.0/1.1 data and reader checks remain intact.

- Before production edits: **14 characterization cases passed** (6 Core, 7
  actual-source component and 1 SQL-backed API case). They pin active-only Main,
  missing contacts, offline reads, retention, actions and access guards.
- Focused snapshot API: **24 passed**, including global retirement with ordinary
  non-Main links excluded, reactivation and per-Location replacement.
- Final tablet Core confirmation: **103 passed**, including all 500 replacement states at the approved
  Location target scale, old-reader compatibility, old snapshot reads, retention
  and six failed-refresh paths. No on-device performance claim is made.
- Actual-source component suite: **11 passed**, including exact identity-block
  wording, active details, neutral reactivation, refreshed replacement, retained
  shops, unchanged actions and existing access guards.
- Complete Release solution build: **0 warnings, 0 errors**.
- JavaScript: **34 passed** (admin, website and delivery-console checks).
- Mocked-Docker shell/Python: **5 passed**, unchanged, in an offline disposable
  Linux container using LF copies of the input files.
- Final Android Release publish passed without compiler warnings/errors. The
  task-specific APK is `.artifacts/wi036-android/com.fieldsales.tablet-Signed.apk`;
  SHA-256 `9192B2DC1D30FA1D77346AB59682ECE775A7E589CCA747B9FAAD672316F607FB`.
- Reflection-disabled import/read/render smoke: passed with
  `JsonSerializer.IsReflectionEnabledByDefault == false`, without changing any
  production or test runtime settings.
- Static renders use the actual Razor source, real SQLite imports and the
  production stylesheet. Active, inactive and reactivated states were inspected
  at 800 × 1100 CSS pixels. The inactive notice remains in the identity block,
  with no separate assignment warning or horizontal overflow; both primary
  actions remain 368 × 64. Replaced HTML is also retained.

The initial complete run recorded one failure in the unchanged ProductSearch
fixture's `ProductPriceApplication.InitializeAsync`: SQL Server reported that
`__EFMigrationsLock` was not held when EF attempted to release it. This happened
before the search assertion or any WI-036 request. The original log/TRX is kept;
the unchanged API project was rerun once after the remaining suite finished.
That complete confirmation passed **143/143**, with the original run settings.
No fixture, assertion, skip or retry setting has been changed to hide this result.

A rebuild attempted while the website test host still held DLLs hit Windows
copy locks. No test host was interrupted. The subsequent sequential rebuild,
after test hosts exited, passed with zero warnings/errors. Final Core and API
confirmations, reflection-disabled smoke and Android publish used the final code.

Raw evidence is under `.artifacts/wi036-*`: characterization/focused/full-suite
TRX, build and Android logs, JavaScript/Python results, reflection-disabled smoke
and rendered HTML/screenshots. No physical tablet was attached. No device
installation or execution is claimed. The task-owned preview server was stopped.
`.artifacts/wi036-initial-suite-summary.json` preserves the original failure;
`.artifacts/wi036-suite-summary.json` identifies every final project's TRX and
the confirmation results explicitly.

## Files and completion boundary

- `FieldSales.Snapshot.Contracts`: additive 1.2 field and generated metadata.
- `TabletSnapshotBuilder.cs`: existing-flag/status projection inside the existing
  Directory read transaction.
- `SnapshotStore.cs`, `SnapshotStore.Locations.cs` and
  `SnapshotStore.LocationContactMetadata.cs`: verified import, generation-owned
  persistence and offline reads, retaining the SQLite schema.
- `LocationDetail.razor`: identity-block text only.
- Existing API, Core and component test projects: focused behavior, compatibility
  and recovery scenarios. No new test project or CI change is needed.
- This report and WI-036's progress entry in `plan-data.js`.

Missing information: none for the approved slice. The console's `workItemStatus`
map is the progress authority. Its generic `T-2.5.2-S` reference has no source
section; the actual task explicitly assigns scenario selection to the agent.
Repository, source paths, .NET 10 MAUI Blazor Hybrid runtime and integration branch
`main` are resolved from approved decisions and predecessor code.

Existing unrelated untracked review documents are untouched. Commits contain the
requested Refs trailer and no automatic attribution. Stop at the green committed,
pushed feature branch; PR creation and integration need a later explicit request.
WI-036 is **done**. Only its console progress entry changed: active at the start,
done after acceptance and final verification completed.
Next by console order is **WI-037 — Warn before tablet sign-out and retain Unsent
work**, informational only; it has not been started.
