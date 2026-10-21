# WI-032 increment 1B — Android-first device storage spike

Approved by the developer on 20 October 2026 for **checkpoint 1B-A only**. This continues
completed increment 1A at `a1b9275`. The generator's results are in
[tablet-offline-spike-results.md](tablet-offline-spike-results.md).

## Selection and developer direction

Resume **WI-032**, the sole active item: Sign in on the tablet and download the day's data
offline. Iteration 1 MVP, T-4.1.1 / REP-US-001 / IAM-US-003. Human Tight-Loop implementation,
Human-Led tests, Complex, High risk (authentication/shared rule and refresh failures), Low
confidence. Dependencies WI-002, WI-021 and WI-005 are done; no explicit open card gate.
Remain on `feature/wi-032-sign-tablet-download-day-s`. Do not repeat 1A or choose another card.

The developer has a **Samsung** and an **iPad mini a few years old**, has **Mac access**, and
said **focus on Android first and I will check the iPad at the end**. This settles the order:
build/test Android, measure the Samsung, then check the iPad before closing the storage spike.
Exact model/OS/ABI/WebView data will be collected during device preflight/reporting. The
developer need not identify them manually before the Android runner is built.

This is an isolated synthetic-data evaluation tool. It evaluates MAUI Blazor Hybrid + SQLite;
it does not implement production tablet sign-in, business capture or the final Sync contract.

### Emulator amendment — 20 October 2026

After USB/Wi-Fi connection attempts, the developer instructed **use an emulator for now as I
can't get them connected to the same network**. This authorizes an interim diagnostic increment
on an explicitly selected emulator: install/launch the Release spike, drive its
synthetic fixture/search/Unsent controls, exercise cancellation and package-only force-close,
and retrieve local reports. Use the loopback fixture host with adb reverse and select
**Emulator bridge (diagnostic)** for transfers. Do not operate either discovered wireless device.

Record emulator identity, storage constraints, all attempted runs and recovery outcomes; stop
for review after this increment. This replaces the immediate need to connect the Samsung,
without completing Samsung checkpoint 1B-B, iPad checkpoint 1B-C or MI-02. The five-repeat physical
device matrix and final storage/runtime decision remain pending. Do not clear other emulator
apps/data or resize/wipe the existing AVD to accommodate a fixture. The initial `emulator-5554`
ran baseline and pre-activation recovery, then lacked space for an APK update. A separate
`WI032_Tablet_API36` AVD (`emulator-5556`) uses the already installed Android 36 image and a fresh
8 GB data partition under `.artifacts/wi032/emulator-avds/`; no existing AVD data is copied.

The developer's subsequent **proceed** accepts that first emulator checkpoint and continues
the already agreed measurement scenarios on the substituted emulator. Run five replacement
refreshes and five offline cold opens per baseline/target/stress profile, with twenty fixed
searches after each refresh. Use the unchanged verified Release APK and retained edited work.
For these repetitions select the host's nominal 10 Mbit/s plus initial 100 ms **application
simulation**, recording it separately from the earlier unthrottled diagnostics; no physical
link target is established. Record every attempted run, median/worst timings, failures and
retained/allocated disk effects. Collect startup/activity logs and a first-observed-ready
polling bound to investigate the slow initial cold open, without labeling that bound exact
first-paint time. Stop for review after this measurement increment; physical acceptance,
1C/MI-02 and production implementation remain pending. Reuse the task-owned AVD only.

## Proposed behavior and storage design

1. A minimal runner selects a fixture server/profile and shows the selected synthetic owner,
   active generation, counts and last completed import. Actions: **Download & apply**, **Cancel**,
   **Search**, **Edit saved test note**, and **Export report**. Test-only pause points allow a
   force-close at download/import/before activation/after activation. The UI stays responsive.
2. Use the existing baseline/target/stress JSON/gzip/manifests. Download to an owner-scoped
   temporary file, check compressed size/hash, stream-decompress and parse one entity at a time,
   and check plain payload hash, format/owner and all manifest counts before activation.
   Bound the parser buffer/row size; oversized rows fail while preserving the active generation.
3. Store snapshot data in SQLite generations. Keep all future payload families as per-entity
   JSON, with separate searchable product/Location projections. This measures full payload
   storage while leaving future business normalization open. Persist staged batches tagged
   with their generation; readers use a captured active generation, never staged rows.
4. Activate only a fully verified generation by one SQLite transaction updating its completed
   state, the active-generation pointer and completion timestamp together. Keep the prior
   generation available. On restart, ignore/reclaim incomplete generations without touching
   the active/retained generation. Serialize refreshes for each synthetic owner.
5. Store captured test work in a **separate owner-scoped Unsent SQLite database**. Seed it once
   from 1A's separate fixture; subsequent refreshes never reseed it. Compare its canonical
   item/payload fingerprint before/after, including an edited note. Retain the original fixture
   file fingerprint too. Do not use SQLite file-byte equality as a proxy for logical work equality.
6. Use SQLite WAL with FULL synchronization, bounded transactions, short-lived readers and
   measured checkpoints. Measure database, WAL/journal, old/staged generation and transfer-file
   disk use. Never copy/rename an open database or detach it from its journal/WAL sidecars.
7. Run SQLite work on a background worker: Microsoft.Data.Sqlite's async methods perform
   synchronous SQLite I/O. Awaiting those methods alone would not keep the tablet UI responsive.
8. Record native SQLite version and native-library/package details on each platform. Add direct
   references for **Microsoft.Data.Sqlite.Core 10.0.10**, the existing SQLitePCLRaw bundle, and
   **SQLitePCLRaw.lib.e_sqlite3 3.53.3** for the new spike helper. The existing test assets resolve
   native SQLite 3.50.3, which precedes SQLite's WAL-reset fix. Explicitly pin/load the newer
   native library for this runner and fail its preflight if a different/unsupported build loads.
   Avoid an unrelated dependency-upgrade sweep across existing application projects.

Proposed benchmark search is case/diacritic-insensitive substring matching over Location name
and Town, with straight/curly apostrophes normalized, stable ordering and bounded visible results.
It is an evaluation query, not approval of the full WI-034 product interaction. Preserve every
raw entity payload; searchable projections are additional data, not a replacement for it.

The design above is proposed for evaluation, not a final production storage commitment.
Its persistence guarantees must be exercised through real process termination/reopen, not
inferred from 1A's directory-publication tests. Force-close tests do not certify every power-loss
or hardware-failure condition.

## Files and implementation work

| Area | Planned paths and responsibility |
|---|---|
| Portable storage/measurement helper | `tools/FieldSales.TabletSpike.Core/`: project, `FixtureManifest.cs`, `SnapshotTransferClient.cs`, `SnapshotRowReader.cs`, `SnapshotSqliteStore.cs`, `UnsentSqliteStore.cs`, `SearchTerms.cs`, `SpikeMeasurements.cs`. No production model dependencies; parse the existing synthetic wire format. |
| Headless adapter | `tools/FieldSales.TabletSpike.Cli/`: project/entry point. Runs the same helper for desktop diagnostics and child-process crash tests; reports clearly identify desktop evidence. |
| Synthetic fixture host | `tools/FieldSales.TabletSpike.Host/`: .NET 10 minimal host with explicit fixture-directory argument and allowlisted manifest/payload/Unsent routes. Loopback port 7206 by default; explicit bind for tablet LAN access. Serve only synthetic exports. Provide test transfer throttling/interruptions and report the configured conditions. |
| Android/iPad runner | `tools/FieldSales.TabletSpike/`: MAUI Hybrid project, `MauiProgram.cs`, platform settings, `Components/Pages/Benchmark.razor`, and native device/report adapters. Distinct package ID `com.fieldsales.tabletspike`. Build Android first; keep the iOS target for the final Mac/iPad check. |
| File/SQLite/HTTP/process tests | `tests/Integration/FieldSales.TabletSpike/`: project and agreed scenario tests. Use actual temporary SQLite files and local HTTP fixture transfer, and terminate only a child process created by the test for crash recovery. Keep the existing 1A scenarios unchanged. |
| Dependency/build wiring | `Directory.Packages.props`: new explicit SQLite provider/native package pins. `FieldSales.slnx`: portable helper, host, CLI and integration tests only. New `FieldSales.TabletSpike.slnx`: native app/helper; existing server builds do not require mobile workloads or a Mac. |
| Commands/evidence | `scripts/tablet-spike.ps1` and this document: release Android build, selected-device preflight/install/launch/force-close/report retrieval and host setup. Generated APK, reports and test evidence stay under `.artifacts/wi032/`. Update the item handoff and add a device-results document. |

Use the installed MAUI 10 template and target Android explicitly on this Windows machine.
Resolve its normal MAUI package versions from the installed workload, pin them in the project
or central file, and record them in the results. Confirm native-library loading in a release
build. If the installed toolchain/device combination cannot run it, stop with a focused amendment.
Respect documented MAUI/Blazor platform/WebView requirements; do not lower the supported OS floor
as a workaround for an older device. Background script helpers use hidden windows; crash tests
terminate only their own explicitly created child process or the selected spike app package.

HTTP access is confined to this synthetic fixture tool. Device platform configuration must be
explicit about the test endpoint; do not add certificate-validation bypasses. Production API,
identity configuration, account administration and production transport policy are out of scope.

## Human Tight-Loop checkpoints

**Approval of this plan authorizes checkpoint 1B-A only**, including its automated scenarios.
It also agrees the proposed later device scenarios, but does not authorize skipping their
human review stops. WI-032 remains active throughout.

1. **1B-A — Build a runnable Android spike.** Agent builds portable import/store/activation,
   synthetic host, headless adapter, Android UI and automated tests; runs a release Android build
   and the repository suites; documents commands and produces an APK. Native app stays outside
   the server solution. Desktop runs validate correctness and report diagnostics only. Commit,
   push and **STOP with the APK/commands ready for Samsung testing**. No iPad setup is needed
   to complete this checkpoint. Do not claim target-tablet performance from these runs.
2. **1B-B — Samsung measurement.** After continuation, the human connects/unlocks the intended
   Samsung and authorizes debugging for that device. Agent reads model/OS/ABI/WebView, verifies
   the target, installs/launches only the spike package, and supplies repeatable commands. Human
   runs/observes touch/search/offline/force-close cases; agent can automate the agreed package's
   lifecycle and retrieve reports. Collect the run matrix below, record failures as well as
   successes, analyse the result and **STOP for Android review**.
3. **1B-C — iPad mini check at the end.** Human uses the available Mac for model/iPadOS/Xcode/
   signing preflight, release build/deployment and the same device scenarios. Agent supplies
   build/run instructions and analyses exported reports. If the older iPad is outside the
   supported runtime/WebView floor, record incompatibility and obtain a decision rather than
   counting Android results as iPad evidence. **STOP for storage/runtime decision 1C** only after
   the agreed iPad result or an explicit scope amendment. No final storage adoption in 1B-A/B.

## Human-Led scenario matrix for approval

Automated tests use small exported fixtures; full-scale capacity/timing runs occur on devices.
Names below are the approved acceptance gate, with data variants where useful.

| Name | Intent | Evidence |
|---|---|---|
| Should_ImportEveryPayloadFamily_When_TheFixtureIsComplete | Actual SQLite content/counts and representative nested data match the manifest; parsing uses bounded records rather than the entire document. | File/SQLite integration; full profiles on device. |
| Should_KeepActiveDataAndUnsent_When_PayloadIsTruncatedCorruptForeignOrUnsupported | Reject length/hash/JSON/schema/owner/count mismatch before activation; current data and edited work survive. | HTTP/file/SQLite integration. |
| Should_KeepActiveDataAndUnsent_When_TransferOrImportIsCancelled | Cancel after real partial transfer/insertion; staged rows never become visible; clean retry succeeds. | Integration plus Samsung Cancel. |
| Should_KeepEditedUnsentWork_When_RefreshingOrReopening | Two Calls, Order and schedule change retain IDs, payloads, captured-under reference and edited note across refresh/restart. | SQLite integration and device. |
| Should_IsolateSnapshotAndWork_When_SelectingAnotherSyntheticOwner | Seed 42 and 77 have separate active generations/work; switching back restores the original owner's data. | SQLite integration and device. |
| Should_RecoverOneWholeGeneration_When_ProcessEndsBeforeOrAfterActivation | Kill the owned child/test app at deterministic activation boundaries; reopen gives the whole old or whole new snapshot with unchanged Unsent work. | Child-process integration, then device force-close. |
| Should_KeepPreviousData_When_StorageOrCommitFails | Inject a write/commit failure and exercise a constrained temporary database; no active-pointer advance or Unsent mutation; retry recovers. | File/SQLite integration and reported device failures. |
| Should_KeepReadsCoherent_When_AnotherRefreshIsRequested | Refreshes serialize; one read uses one generation; staged/retired cleanup cannot mix or delete the active owner's data. | SQLite integration. |
| Should_ReopenDownloadedData_When_TheDeviceHasNoConnection | Physical offline cold launch uses SQLite and exposes saved test work without a request to the host. | Samsung, then iPad. No 24-hour production auth rule here. |
| Should_ReturnExpectedMatches_When_SearchingRepresentativeNamesAndTowns | Fixed twenty terms include apostrophes, accents, partial words, case variants and no match; report correct results plus query and input-to-visible time. | Correctness integration, then device timing. |
| Should_IdentifyActualConditions_When_ExportingAMeasurementReport | Record physical/emulator/desktop, model/OS/ABI/WebView, app/native SQLite version, hashes/counts, cold/warm state, phase/disk/memory metrics and network conditions; incomplete runs stay incomplete. | Report integration and device inspection. |
| Should_LoadThePinnedNativeEngine_When_TheReleaseRunnerStarts | Runtime SQLite/native settings match the tested/pinned configuration; unsupported native version is an explicit preflight failure. | Desktop integration, release Android smoke, later iPad. |

New helper/host behavior is additive. Run the existing 1A characterization scenarios before
connecting it; do not change generator output/fixtures to accommodate importer bugs. Changes to
production identity/API behavior are not part of this plan.

## Device measurements and review targets

- Reuse 1A's three scales: 5,000 products/500 Locations/60,000 historical lines; evaluation
  25,000/500/120,000; stress 100,000/2,000/1,440,000. Import the full future payload each time.
- On Samsung first, run five measured refreshes and five cold offline reopens per profile,
  with at least twenty fixed search terms. Repeat on iPad at the end. Separate fresh import
  from refresh with an existing generation; report all runs, median and worst, cache state and
  failure reasons. Stress can fail safely; do not count a failed run as a fast successful run.
- Required source target: **500-Location input-to-visible results under one second**. Candidate
  review targets remain **60-second evaluation refresh** and **3-second offline reopen**.
- Measure HTTP transfer, verification/decompression/import/indexing/activation separately and
  total tap-to-completion. Network test target remains 10 Mbit/s and 100 ms RTT, plus a 2 Mbit/s
  degraded diagnostic. Host rate limiting/initial delay is labeled application-level simulation;
  it is not proof of physical link throughput/RTT. Reports distinguish actual LAN measurements,
  simulations and any independently shaped/verified link. Do not claim the exact network target
  passed unless the conditions were established.
- Include snapshot database/indexes, WAL/journal, retained/staged generations and transfer file
  in steady/peak disk use; record process memory where platform tooling supplies it. Mark an
  unavailable metric unavailable. Native wall-clock and web-render timing use suitable local
  clocks, not subtraction between unrelated clock domains.
- Keep queries/SQLite work off the UI thread. Device performance is measured in Release with
  the version/configuration recorded; emulator/desktop results remain diagnostic.

## Verification and stop conditions

At 1B-A, run agreed integration/process tests and the complete existing .NET/JavaScript suites;
use bounded `sqlserver.runsettings` and only the standing NU1902/NU1903 audit exceptions.
Build the release Android target separately, inspect APK/native assets, record any install/smoke
evidence truthfully, and stop committed/pushed with no unrelated user files staged. Native
installation/timing is not a prerequisite for declaring the **build checkpoint** complete;
it remains required for the **device spike** and MI-02.

No new production status transition: WI-032 is already active. Do not set done at any 1B
checkpoint. If package/runtime compatibility or storage behavior invalidates this plan, stop
and propose a focused amendment before changing the design or weakening a scenario.

## Missing information, assumptions and scope

No further human input is required to start **1B-A once approved**. Samsung model/OS/ABI are
measurement inputs captured at 1B-B. Mac access is confirmed; exact iPad generation/iPadOS and
Xcode/signing compatibility will be checked at the final iPad stage, as requested.

- The connected Samsung is the intended tablet only after the human identifies/connects it.
  Do not guess among multiple connected devices. An unsupported OS/WebView needs review.
- Native-package loading and MAUI release packaging must be verified; NuGet compatibility
  labels alone do not prove runtime compatibility. No claim of universal Samsung/iPad support.
- The generational JSON-plus-search-projection layout, WAL/FULL settings, SQLite provider/native
  pins, package ID and port are **(inferred/proposed)** implementation choices to approve here.
  The benchmarks evaluate this layout; 1C decides production storage/normalization and budgets.
- Physical network shaping is not yet provisioned. Local transfer simulation is allowed for
  diagnostics and must be labeled; exact-link-budget acceptance remains a measurement condition.
- The iPad mini may require a newer OS or a revised runtime choice. Its final check is retained;
  Android success alone cannot close both-device feasibility.
- MI-01 production runtime/layout, MI-02 final capacity/storage acceptance, MI-08 production
  I-03 layout, MI-62 disabled-account treatment, production address source/contract ownership/
  version compatibility and 24-hour local-session rules remain their later decision gates.
- Out of scope: production OIDC/Sync endpoints, actual Call/Order capture (WI-038 and later),
  upload-first/exactly-once work (WI-039), full Unsent/failure/recovery UI (WI-040–042), restricted
  catalogue authorization (WI-043), and the production Location search/screen (WI-034/035).
- Production snapshot/Unsent encryption, account disablement and credential storage are not
  settled by a synthetic fixture app. No real staff or business data is used in this spike.

## Proposed next diagnostic increment — pending review

The repeat results expose an after-activation readiness timeout and expensive diagnostic scans.
This proposal is **not approval to change the runner during the current measurement matrix**.
Keep the old APK/reports and their failed/incomplete outcomes separate from any rebuilt runner.
Begin with the unchanged 26 portable cases and characterization of correct status, preserved-work
and physical-disk reporting behavior before changing those paths.

1. In `tools/FieldSales.TabletSpike.Core/SpikeWorkspace.cs` and `SpikeMeasurements.cs`, keep
   inexpensive physical database/WAL/SHM/transfer/Unsent samples in the timed path. Make logical
   per-generation payload scanning an explicitly optional, separately timed diagnostic; report
   unsampled logical totals as unavailable. Do not turn missing measurements into zero values.
2. In `SnapshotTransferClient.cs` and `SnapshotSqliteStore.cs`, persist which operation is pending
   and distinguish generation deletion, checkpointing and report finalization clocks. Preserve
   incomplete state if the process stops during any of them. Keep atomic activation, owner
   isolation, active/prior retention policy and native WAL/FULL configuration unchanged.
3. In `tools/FieldSales.TabletSpike/Components/Pages/Benchmark.razor`, export the report without
   requiring a logical scan first and label operation/UI completion scopes accurately. Retain
   the local-ready startup guard. Diagnose the retention/checkpoint interval before proposing
   a storage-policy or deletion-algorithm change.

Human-Led scenarios to approve before that increment:

- Reports distinguish sampled physical bytes from unavailable/optional logical totals; a large
  downloaded day can be exported independently of the optional scan.
- A process stop after activation while retention/checkpoint/report finalization is pending
  retains the whole active generation and edited Unsent work; its saved refresh stays incomplete
  and identifies the pending phase. Reopening does not manufacture a successful refresh report.
- Phase and enclosing-operation clocks retain their stated scopes, including failed/incomplete
  runs. The existing twenty-term search and hash/count checks remain unchanged.
- All existing 26 portable scenarios and repository suites pass after source changes. Publish
  and inspect a new Release APK, then run one focused stress replacement/recovery diagnostic
  with the unchanged fixtures. Record any timeout and stop for review before repeating the full
  matrix or making another storage change. Physical Samsung/iPad acceptance and 1C stay pending.

## Primary guidance checked

- [MAUI supported platforms and iOS Mac requirement](https://learn.microsoft.com/en-us/dotnet/maui/supported-platforms?view=net-maui-10.0)
  and [device information APIs](https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/device/information?view=net-maui-10.0).
- [Incremental UTF-8 JSON reading](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/use-utf8jsonreader).
- [SQLite bundles/providers](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/custom-versions),
  [synchronous behavior of SQLite async methods](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/async),
  and [transactions](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/transactions).
- [SQLite atomic commit](https://www.sqlite.org/atomiccommit.html) and [WAL, durability and WAL-reset fix](https://www.sqlite.org/wal.html).
  The documented reset fix is in 3.51.3+ (with selected backports); current repo test assets
  resolve 3.50.3. [Native package 3.53.3](https://www.nuget.org/packages/SQLitePCLRaw.lib.e_sqlite3/3.53.3)
  is the approved explicit native reference now used by the spike; see the build/results document.

The developer's **approve** accepted the scenario matrix/design and authorized 1B-A. This approval
boundary follows the [console-delivery-next-item skill](C:/Users/colmk/.codex/skills/console-delivery-next-item/SKILL.md):
“they must be agreed as part of plan approval **before** implementation begins.”
