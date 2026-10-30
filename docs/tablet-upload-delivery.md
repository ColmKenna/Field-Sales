# WI-039 — Upload finished work first, each item exactly once

T-4.1.2 / REP-US-001 S1 (upload), S5 and the edge case "partial uploads never duplicate items", from `main` at
`5807676` on `feature/wi-039-upload-finished-work-first-item`.

## Approved scope and decisions

The developer approved the plan, decisions D1 to D11 and the Human-Led scenario matrix with **Approved** on
29 October 2026. After increment 1 they said "Continue all steps, if there is anything you are unsure about ask me
now", and answered three questions on 30 October 2026: accept the A5/A6 stand-ins, add the directory and ordering
migration bundles, and open a pull request at the end.

| # | Decision |
| --- | --- |
| D1 | An item's identity across retries is the id the tablet gave it at capture. The server keeps a SHA-256 of the body, but it is not part of identity. The same id with a different body keeps the first version, counts the re-send and is still confirmed. |
| D2 | One `PUT` per item. The server answers 201 `Received` the first time and 200 `AlreadyReceived` after; the tablet marks the item Sent on either, straight away, before the next item. A lost confirmation is recovered by sending again. That is the whole protocol. |
| D3 | Saved Calls go first, then Ready to Send Orders, oldest first in each. The server does not check the link between a Call and its Order. |
| D4 | **MI-52:** the technical faults are `corrupt`, `unsupported`, `incomplete`, `not-yours` (400), `id-in-use` (409) and `too-large` (413, over 1 MiB). Business rules are never checked (BR-NEW-006). A refused item stays unsent and the Sync carries on. |
| D5 | The tablet's Unsent database (format 2) moves a confirmed item from `work` to a `sent` table in one transaction. |
| D6 | The server stores work in the new `OrderingDb`: a `ReceivedWork` ledger for every kind and an `Orders` table where a received order starts as **Pending**. |
| D7 | A Call's contents are left to Record Call (WI-047). The server stores any JSON object whose `id`, when present, matches. |
| D8 | Sync runs: token, uploads, space check, download. The download runs whatever happened to the uploads, unless the sign-in or permission was refused. |
| D9 | "Sending 2 of 3…", then "Downloading your day…". Everything sent goes to Home; otherwise the rep stays on Sync and reads "N items not sent. They'll send next Sync." |
| D10 | Orders stop at Pending. No head-office screen. |
| D11 | The upload contract is `TabletUploadContract` in `FieldSales.Snapshot.Contracts`. The snapshot document and its version are unchanged. |

## What was built

**Server (`FieldSales.Api`)**

- `Ordering/OrderingDbContext.cs`, `ReceivedWork.cs` and migration `AddReceivedWorkAndOrders`: the ledger keyed by the
  tablet id (rep, receive time, SHA-256, body verbatim, re-sent-with-changes count) and `Orders` (status, Location,
  Ordered By, capturing rep, date, Call, snapshot reference, receive time) with a foreign key to the ledger.
- `Ordering/TabletWorkReceiver.cs`: checks the item's own structure only, then stores the ledger row and the Pending
  order in one `SaveChanges`. A unique-key violation from a simultaneous copy is resolved to `AlreadyReceived`.
  An order must name the signed-in caller as its capturing rep.
- `TabletSync/TabletSyncEndpoints.cs`: `PUT /tablet/uploads/orders/{id}` and `PUT /tablet/uploads/calls/{id}` under the
  existing `TabletSync` policy. A refusal is a problem response with an `UploadFaults` code in its `fault` extension.
- AppHost and `DemoProfile` add `OrderingDb`. `scripts/Create-MigrationBundles.ps1` and the README add the `directory`
  and `ordering` bundles.

**Contract (`FieldSales.Snapshot.Contracts/TabletUploadContract.cs`)**: paths, the 1 MiB limit, `UploadReceipt`,
`UploadReceiptStatus` and `UploadFaults`, with a source-generated JSON context for trimmed tablet builds.

**Tablet (`FieldSales.Tablet.Core`, `FieldSales.Tablet`)**

- `UnsentStore` format 2 (upgraded on open): `MarkSent`, `ReadSent`, `IsSent`. Everything that reads unsent work
  (counts, the sign-out warning, shop retention, the fingerprint) now leaves Sent items out with no change of its own.
- `WorkUploader`: builds the queue, sends each item exactly as saved, and marks it Sent only for a receipt that names it.
  400/409/413 leave the item unsent and move on; 401/403 stop the Sync; a lost connection, a 5xx or any other answer
  stop the remaining uploads.
- `TabletSync.SyncAsync` uploads before the space check and the download, and reports `SyncOutcome.Upload`
  (`UploadSummary`) and `UploadProgress`. `OrderStore` refuses a Sent order's id.
- `Sync.razor` shows "Sending 2 of 3…" with a progress bar, then "Downloading your day…", and the not-sent line.

## Acceptance criteria

1. **"With 2 saved Calls, 1 Ready to Send and 1 In Progress Order, Sync uploads the 2 Calls and the Ready to Send Order,
   each marked Sent as the server confirms it; the In Progress Order stays unchanged; then the snapshot downloads (S1)."**
   `TabletUploadTests.Should_UploadTheTwoCallsAndTheReadyOrderMarkEachSentThenDownload_When_ISyncWithSignal`: the three
   arrive in order, each is Sent, the In Progress body is byte-identical, the download is the last request, a new
   snapshot is active and the result reads "Last synced 07:42". Marking happens per confirmation (T2, T4, mutation checks).
2. **"An order for a product that became unavailable after the snapshot is accepted and marked Sent; head office handles
   the line (S5; BR-NEW-006)."** `TabletUploadApiTests` A5 (server, line unchanged, Pending) and
   `TabletUploadEndToEndTests.Should_AcceptTheOrderAsPendingAndMarkItSent_When_ItsProductBecameUnavailableAfterTheMorningSync`
   (real tablet store against the real API: Pending on the server, Sent on the tablet, and the new snapshot no longer
   offers the product). The receiver never reads the catalogue. Handling the line is head office's (WI-067, area 3).
3. **"Partial uploads never duplicate items (edge case)."** Server: A2 (re-send), A3 (re-send with changes), A4 (six
   copies at once, one stored). Tablet: T2 (connection drops after one), T3 (lost confirmation re-sent next Sync,
   confirmed once), T4 (app killed between the server's confirmation and marking Sent, using the crash host).

## Scenario matrix and tests

| Area | Tests | Where |
| --- | --- | --- |
| Server A1–A12 | 29 (theories expanded) | `tests/Integration/FieldSales.Api/TabletUploadApiTests.cs` |
| Tablet T1–T12, plus 401/403 stop and progress | 18 | `tests/Integration/FieldSales.Tablet.Core/TabletUploadTests.cs` |
| Screen C1–C2 | 5 | `tests/Integration/FieldSales.Tablet.Components/TabletSessionScreenTests.cs` |
| End to end E1–E2 | 2 | `tests/Integration/FieldSales.Api/TabletUploadEndToEndTests.cs` |

**Stand-ins (accepted 30 October 2026).** Products can't be archived, Ranges don't exist and Locations can't be archived.
A5 and E1 make the product unavailable by moving it into a Restriction Group after the snapshot and archiving the group,
which hides it from every rep. A6's "archived" case archives the shop's Town. Because the receiver reads only the item,
the real cases need no further server work when those features arrive.

**Changed existing assertion.** `TabletStorageScenarios.Should_RefuseSyncAndKeepData_When_FreeSpaceIsBelowTheCheck` now
asserts that no snapshot was requested, rather than no request at all, because D8 sends uploads before the space check.

**MI-62** (an entire account disabled while work is unsent) stays **undecided**. Nothing was built for it.

## Mutation checks

- Removing the unique-violation catch in the receiver failed A4 in three of three runs.
- Marking an item Sent without a confirmation failed the three refused-item cases.
- Marking Sent before the `upload-confirmed` boundary failed T4.

## Final verification

On the final code (30 October 2026, Docker running):

- `dotnet build FieldSales.slnx -c Release -m:1 --no-incremental`: 0 warnings, 0 errors.
- `dotnet test FieldSales.slnx -c Release --no-build -m:1 --settings sqlserver.runsettings`: **2,805 passed, 0 failed,
  0 skipped** across 12 projects (baseline 2,751): API integration 174, API unit 185, Tablet Core integration 171,
  Tablet Components 49, Tablet Core unit 7, Web integration 873, Web unit 219, Identity Admin integration 804 and unit 256,
  DemoData 4, SnapshotSpike 22, TabletSpike 41.
- `scripts/tablet-app.ps1 -Action Build -Configuration Release`: the signed Android APK builds.
- `node --test plan_docs/field-sales-delivery/tests/status-store.test.js`: 11 passed. The only console change is
  `WI-039` → `done`.
- One WI-033 test (`ProductAttributeIntegrationTests`) saved a Call to prove a download leaves unsent work alone. Sync now
  sends Calls first, so it saves a schedule change instead, which Sync never sends; its assertions are unchanged.

## Not verified here

- No device run. "Sending 2 of 3" and the progress bar were not seen on the Samsung; the bar's wrap uses CSS `:has()`.
- **Migration bundles don't run on this machine.** All seven bundles build, but every one, including the existing
  staff-web and catalogue bundles, fails at run time with "Could not load file or assembly
  Microsoft.EntityFrameworkCore.SqlServer, Version=10.0.10.0". This predates WI-039 and was not fixed here.

## Left for later items

- WI-040: why items weren't sent ("poor connection", "Sign in again to send 3 items"), and the download-after-signal case.
- WI-042: Needs Attention for an item refused with an `UploadFaults` code (it is retried at every Sync until then).
- WI-046 and Call corrections: what happens to an item changed after an upload whose confirmation was lost (D1).
- WI-047: the Call body, its structure checks and its snapshot reference (increment 2 for Calls).
- WI-067: line tables and acceptance at the cut-off; orders wait as Pending.
- The Sent Item view and Sent statuses in the snapshot; Sent items no longer keep their shop's details on the tablet.
