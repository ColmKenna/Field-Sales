# WI-035 — Open a shop's Location screen on the tablet

T-4.2.2 / REP-US-004, on `feature/wi-035-open-shop-s-location-screen` from
`main` at `10c4fb5`. The developer approved the plan, scenario list and Apply,
then reviewed both rendered states and resumed with `Continue T-4.2.2` on
29 October 2026. See [the checkpoint](tablet-location-detail-checkpoint.md).

## Delivered behavior

The existing `/locations/{Id}` destination now shows the shop's downloaded address,
Main Contact and contact details. Record Call and New Order have equal primary
styling and touch targets, side by side. The agreed temporary behavior explains
that each feature is not available yet. Recent Calls and Orders explicitly say
their history is not available on this tablet yet; populated history waits for
its source. These are developer-approved amendments, not invented sample history.

Unsent work can reference a shop with a `locationId` GUID string. A small rep-bound
identity cache preserves its latest downloaded details through later snapshots,
normal generation cleanup and restart. It does not rewrite Unsent payloads or
infer IDs from names. Reassigned retained shops show the source notice and offer
neither new-work action. They remain absent from assigned-only search and Home.
If a shop becomes assigned again, its current downloaded identity and actions win.

The existing shared access guard clears details before checking the current rep
and the 24-hour window. Late worker reads cannot restore an older route's details.
Local read failures show a controlled message while preserving saved work. Final
review added an explicit `InvalidDataException` catch: that exception inherits
`SystemException`, not `IOException`. The final Release component tests and APK
include that correction; the approved layout is unchanged.

No API endpoint, wire snapshot version, SQLite schema or package version changed.
The identity cache stays inside the existing Android backup-excluded app storage.

## Acceptance criteria

1. **“Opening Doyle's Shop shows its address, Main Contact, recent Calls and Orders,
   and Record Call and New Order as equal actions side by side.”** Address and active
   Main Contact are read from SQLite and verified by Core/component tests. Address
   follows the approved Town/County/Eircode data, without invented street lines.
   The reviewed 800 × 1100 render measures both actions at 368 × 64 CSS pixels on
   the same row, with matching primary colour. The developer-approved amendment
   uses explicit unavailable-history states and explanatory action messages until
   those later features exist. Both actual button events are tested.
2. **“If Doyle's Shop was reassigned away in the new snapshot and the rep has an
   Unsent Order for it, the screen shows \"No longer assigned to you — your existing
   order will still be sent\" and offers neither New Order nor Record Call.”** The
   Core test retains the latest contact/address after four further refreshes,
   cleanup and reopen, with unchanged work bytes/fingerprint. The component test
   and reviewed render verify the exact notice and zero new-work buttons.
3. **“A Location with no recent calls or orders shows none without error.”** Under
   the approved amendment, absent history data produces explicit unavailable
   states rather than asserting that no Calls or Orders exist. The component
   scenario renders both messages without error. Missing Main Contact data is
   also handled and tested without inventing contact details.
4. **“Recent Calls and Orders open their read-only views once those exist
   (T-7.5.1); until then they are listed only.”** Those views and the history source
   do not exist. Per the approved amendment, the Recent region shows unavailable
   states with no false records or broken history links. The component test pins
   this behavior. Populated rows and their later destinations remain deferred.

Visit Dues, GPS, closures, inactive-contact wording (WI-036), Call capture, Order
building, populated history/read-only item views and MI-62 remain outside this item.

## Agreed scenario coverage

| Scenario | Verification |
|---|---|
| Existing assigned search and saved work after replacement | Characterization passed before production edits |
| Assigned shop identity and equal actions offline | Real SQLite Core test, actual-source component test, reviewed render |
| No downloaded Main Contact | Core and component scenarios |
| History unavailable until its source exists | Actual-source component scenario |
| Either primary action tapped | Two real component event cases; explanation shown, no broken navigation |
| Reassigned shop with an Unsent Order | Retention scenario plus exact notice/no-buttons component scenario |
| Repeated Syncs, cleanup and restart | Four subsequent refreshes and reopen; latest downloaded identity retained |
| Retained shop excluded from assigned-only search | Core scenario verifies both search and Home list |
| Expired access, rep change, overlapping navigation | Actual production guard/component with controllable MAUI boundary |
| Legacy work without `locationId` | Stored records and fingerprint remain identical |

## Verification

The complete application run passed **2,645 .NET tests, 0 failed, 0 skipped**
across all 12 test projects, without retries. Every TRX result was reconciled to
its project's counters and all outcomes are Passed. No existing test was weakened,
skipped or deleted.

| Project | Passed |
|---|---:|
| API integration | 140 |
| Demo data integration | 4 |
| Identity integration | 804 |
| Snapshot spike integration | 22 |
| Tablet component integration | 7 |
| Tablet Core integration | 91 |
| Tablet spike integration | 41 |
| Website integration | 873 |
| API unit | 182 |
| Identity unit | 256 |
| Tablet Core unit | 6 |
| Website unit | 219 |

Command: `dotnet test FieldSales.slnx -c Release --no-build -m:1 --settings
sqlserver.runsettings --blame-hang-timeout 2m --logger trx --results-directory
.artifacts/wi035-test-results`. SQL Server cases used real disposable containers.
The final corrected component assembly was built before the complete run reached
that project. The final whole-solution build also passed with that source.

- Full Release solution build: zero warnings and errors.
- Final actual-source Release component suite: 7 passed, none failed/skipped.
- Application JavaScript: 23 passed (18 admin, 5 website).
- Delivery-console status-store JavaScript: 11 passed.
- Shell/Python: 5 passed unchanged in an offline disposable Linux container with
  LF copies and a mocked Docker executable; no real Docker storage is reset.
- Final Android Release publish passed. APK SHA-256:
  `77E22C06C65CA5C14C2E653A682A4BC290B8DC3509212ACE06C887B2A50759D1`.
  The final publish had no compiler warnings/errors. The earlier checkpoint's
  transient linker-host failure and successful retry remain recorded there.
- Every integration test project appears exactly once in the CI matrix, including
  the new component project in the supporting lane.

Raw evidence is under `.artifacts/wi035-*`: final solution/full-suite logs, TRX results,
component checks, JavaScript/Python logs, final Android publish log and rendered
checkpoint fixtures/images. No physical tablet was attached; no device execution
or installation is claimed. The temporary preview server was stopped.
The aggregate is `.artifacts/wi035-suite-summary.json`.

## Files and handover

- Tablet Core: Location read model, rep-bound retained identity file, synchronized
  capture/activation and the existing workspace/store integration points.
- Tablet app: existing Location component, `TabletApp` read entry and stylesheet.
- Verification: Core characterization/behavior cases and a desktop project that
  compiles the production Razor/guard sources and dispatches their real events.
- Solution and CI: include the component tests without requiring a MAUI workload.
- Documentation: checkpoint and this delivery report. Inside `plan-data.js`, only
  WI-035's `workItemStatus` entry changes.

Missing information: none for the approved scope. Existing unrelated untracked
review documents are untouched. Commits use the requested Refs trailer and no
automatic attribution. Integration remains the developer's decision.

WI-035 is **done**. Its console status changed only at implementation start
(`active`) and after the acceptance/verification gates passed (`done`).

Next by console order is **WI-036 — Show an inactive main contact on the tablet**,
informational only; it has not been started.
