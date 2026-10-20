# WI-032 checkpoint 1B-A — Android build and correctness evidence

20 October 2026. The developer approved the [1B plan](tablet-offline-device-spike-plan.md),
including twelve named scenarios, for checkpoint **1B-A only**. This document is the build
handoff. Samsung measurements (1B-B), iPad mini verification (1B-C) and the runtime/storage
decision (1C) are still pending. WI-032 remains active; MI-02 is open.

## What the runner evaluates

The isolated MAUI Blazor Hybrid app uses package `com.fieldsales.tabletspike`, synthetic
owners and the unchanged 1A fixtures. It has no production identity/API/model references.
The server solution contains only its portable helper, headless adapter, fixture host and
integration tests. `FieldSales.TabletSpike.slnx` is the separate mobile solution.

The shared importer checks the manifest, raw gzip length/hash, streamed plain length/hash,
owner/schema/date/seed, all nine payload families and all fourteen manifest counts. Its
parser grows from 64 KiB to at most 1 MiB; one larger entity fails safely. Per-entity JSON
retains nested future payload fields. Product and Location search projections are additional
data. Inserts commit in batches of 128 into an invisible staged generation; one transaction
marks it complete and changes the active pointer/timestamp. Incomplete generations are
reclaimed at the next serialized import. The active and prior completed generation remain;
WAL read transactions retain a coherent captured view through retirement.

Each synthetic owner has separate snapshot and Unsent SQLite files in app data. The four
saved fixture items seed once. Editing the Order note changes its canonical payload fingerprint;
refresh/reopen must preserve that fingerprint, IDs, captured-under reference and original
fixture SHA-256. SQLite uses WAL/FULL, non-pooled short connections and passive checkpoints.
There is no live-file copying or renaming. This does not establish production encryption,
credential policy, power-loss guarantees or survival of uninstall/cleared app data.

Reports distinguish desktop/emulator/physical device, actual model/OS/ABI/WebView, build,
native engine/configuration, fresh/replacement state and configured transport/simulation.
They retain failed/incomplete status. Force-close pause points write an incomplete report
before blocking; after restart, **Export saved refresh** can retrieve it. A separate
offline-open report does not turn an interrupted refresh into a successful one.

Disk samples include database/index allocation, WAL, journal, shared-memory sidecar, transfer
file and the separate Unsent store. Per-generation raw JSON totals are logical payload bytes,
not extra physical bytes. Per-index allocation is explicitly unavailable; it is included in
the database total. Peak is the **largest sampled** total, not an assertion of a continuously
observed filesystem high-water mark. Memory is a labeled end-of-run WorkingSet64 sample when
available, not peak RSS. The pipelined import reports total and measured subsets (decompression
read/hash, row count/insertion/index maintenance); do not add these subsets to their enclosing
total. Parsing remainder includes dispatch/measurement overhead.

Search normalizes case, diacritics and straight/curly apostrophes, sorts deterministically,
and bounds displayed rows. Query time uses Stopwatch. Manual Search records DOM submit to
completed render plus two animation frames on the WebView's `performance.now` clock; automated
terms start at a JS invocation. It does not measure every typed keystroke. Android `am start -W`,
database reopen and WebView timings have distinct scopes; do not subtract/add unrelated clocks
or call any one of them full input-to-visible cold-launch time. Human device observation remains
necessary for the agreed search/reopen targets.

## Build and verification

- Existing 1A characterization: **22/22 passed unchanged** before connecting the importer.
- Approved portable scenarios: **26 cases passed** across all twelve scenario names, including
  malformed/foreign/oversized payloads, cancellation after actual transfer/insertion, edited
  Unsent retention, owner separation, owned-process termination before/after activation,
  injected write/commit errors, real SQLITE_FULL via constrained pages, coherent readers,
  no-network reopen, twenty search terms, report conditions and native configuration preflight.
- Desktop preflight loaded **SQLite 3.53.3**, WAL, synchronous=2 (FULL), THREADSAFE=1.
  The core directly pins Microsoft.Data.Sqlite.Core 10.0.10, SQLitePCLRaw bundle 3.0.0 and
  native lib 3.53.3. Source-generated JSON metadata is used for trimmed mobile serialization.
- Full baseline fixture (16,441,340 plain bytes) imported on desktop with a **65,536-byte**
  parser buffer. This is correctness/diagnostic evidence, not a target-tablet performance run.
- Full .NET solution non-incremental build: **0 errors**, 79 standing NU1902/NU1903 audit
  warnings only. Subsequent final source build: 0 errors, 45 of the same warning codes.
- JavaScript: **34 passed** (admin 18, product form 5, console 11); fifteen scratch source,
  package and test files match the repository byte-for-byte. No vendored assets changed.

- Complete .NET suite: **2,343 passed, 0 failed/skipped**, nine projects (the existing 2,317
  plus 26 new spike cases). Every project ran with the bounded SQL Server settings. Final
  report-I/O and retention-timing corrections were followed by another **26/26** affected
  scenario run and a rebuilt Release APK; no existing application or scenario was changed.
- Android Release publish: **succeeded with no build warnings**. The final 44,617,983-byte APK is
  `.artifacts/wi032/1b-android/com.fieldsales.tabletspike-Signed.apk`, SHA-256
  `e3e95808229c151cadc77f01a1d936b92f6bb5581fffc3593dc29d65b5bca659`.
  Package/launcher inspection confirms API 24 minimum, API 36 target and ARM64, ARMv7,
  x86_64 ABIs. APK v2/v3 signature and 16 KiB native zip alignment checks pass. The three
  embedded `libe_sqlite3.so` files each exactly match their pinned 3.53.3 NuGet asset.
  This verifies packaging, not Android runtime loading; that preflight runs on Samsung in 1B-B.
- Successful build toolchain: SDK **10.0.401**, Android workload **36.1.69**, Android SDK/build
  tools **36.0.0**, MAUI packages/SDK **10.0.20**, Mono runtime/AOT **10.0.12**. Release uses
  trimming/AOT with explicit JSON metadata and development signing. No physical install/smoke
  run occurred. Target-tablet compatibility, rendering and performance remain unverified.

Evidence is under ignored `.artifacts/wi032/`: `1b-characterization/`, `1b-focused-rerun/`,
`1b-full-build.log`, `1b-final-build.log`, `1b-full-tests.log`, `1b-full-verification/*.trx`,
`1b-verification-summary.json`, `1b-*-tests.log`, `1b-android-build-recovered.log`,
`1b-apk-native-libraries.json`, `1b-final-apk.json`, `1b-final-apk-signature.log`,
`1b-final-focused.log`, `1b-reviewed-source-build.log`, `1b-android-build-final.log` and
`1b-android/`. No device was selected, installed,
force-closed or measured during 1B-A.

### Workstation build recovery

The installed manifests initially requested Mono 10.0.13 while the installed packs were
10.0.12. `dotnet workload restore --skip-manifest-update` nevertheless selected a newer
workload set and expanded across the registered workloads. It exhausted C: and failed;
its rollback reported some removal errors and left new manifest directories behind.
Do not repeat that broad restore to reproduce this build. The failed logs and verified
backups of the six new platform manifest directories are in
`.artifacts/wi032/failed-workload-restore/` and `1b-workload-restore.log`. Ordinary cleanup was
denied by Windows ACLs. A reviewed administrator script subsequently verified every backup
hash and removed just those six task-created directories. `dotnet workload list` again shows
the original versions/hash `10.0.400-manifests.378ba522`; C: has over 8 GB free at verification.
Evidence is `workload-manifest-cleanup-result.json` and the fixed-list cleanup script under
`.artifacts/wi032/`. No original manifest, arbitrary temp directory, IDE process or registry
entry was removed. MSI rollback reported some package-removal errors; the confirmed recovery
claim is restored manifest selection and a successful build using the original packs.

The successful build uses unchanged copies of the original matching installed manifests,
selected through process-scoped `DOTNETSDK_WORKLOAD_MANIFEST_ROOTS`, and puts linker temp files
on D:. No installed manifest content was edited, no OS floor was lowered, and no unrelated
files were deleted. The script restores its environment variables after building. On this
workstation prepare/use that snapshot as follows; on another machine use its correctly
installed matching toolchain rather than assuming these old pack versions are present:

```powershell
pwsh -File scripts/tablet-spike.ps1 -Action UseInstalledPacks
pwsh -File scripts/tablet-spike.ps1 -Action Build -ManifestRoot .artifacts/wi032/installed-manifests
```

## Samsung commands for checkpoint 1B-B

Run commands from the repository root. Build commands do not select/start a device:

```powershell
pwsh -File scripts/tablet-spike.ps1 -Action Build
pwsh -File scripts/tablet-spike.ps1 -Action Host
```

The fixture directory defaults to `.artifacts/wi032/final-profiles`, with allowlisted
`baseline/`, `target/`, `stress/` exports and `unsent-42.json`. For another synthetic owner,
generate profiles under `<fixture-root>/77/<profile>` and `77/unsent-77.json`; seed 42 may use
either the root layout or `42/`. Fixture endpoints are
`/fixtures/42/baseline/{manifest|payload|unsent}`. Arbitrary filenames/routes are not served.

For LAN access run the host with `-Bind 'http://0.0.0.0:7206'`, then enter the computer's
private LAN address in the app. The script changes no firewall rules. The app explicitly
allows HTTP for this synthetic test tool and makes no certificate-validation bypass.
Optional `mbit`, `delayMs`, and `stopAfter` host parameters simulate application transfer
conditions; they do not establish physical throughput or round-trip latency.

After continuation authorizes 1B-B, connect/unlock the intended Samsung, enable USB debugging
and accept its computer authorization. Use its actual serial from `adb devices -l`:

```powershell
$tabletSerial = '<the Samsung serial>'
pwsh -File scripts/tablet-spike.ps1 -Action Preflight -DeviceSerial $tabletSerial
pwsh -File scripts/tablet-spike.ps1 -Action Install -DeviceSerial $tabletSerial
pwsh -File scripts/tablet-spike.ps1 -Action Launch -DeviceSerial $tabletSerial -Cold
```

Preflight collects model/Android/API/ABI/WebView automatically. The app requires API 24+;
review an unsupported device instead of lowering the floor. The command always requires
an explicit serial and verifies the APK package before installing. The APK uses Release
configuration with local development signing; production signing is outside this spike.

For a USB-only diagnostic, leave the host loopback-bound, run the following command, enter
`http://127.0.0.1:7206/` in the app and select **USB adb reverse** as transport. Such a run
does not prove the physical LAN network target:

```powershell
pwsh -File scripts/tablet-spike.ps1 -Action Reverse -DeviceSerial $tabletSerial
```

In the app, select profile/owner/transport; Download & apply; edit the saved Order note;
refresh and compare fingerprints. Search manually and run the twenty-term diagnostic.
Export each run (including failures). For deliberate crash recovery, select a test pause,
start the refresh, wait for the displayed boundary, then:

```powershell
pwsh -File scripts/tablet-spike.ps1 -Action ForceClose -DeviceSerial $tabletSerial
pwsh -File scripts/tablet-spike.ps1 -Action Launch -DeviceSerial $tabletSerial -Cold
pwsh -File scripts/tablet-spike.ps1 -Action PullReports -DeviceSerial $tabletSerial
```

Tap **Export saved refresh** after a restart to preserve the interrupted report, and export
the separate reopen report. Exports are also written to the app's external reports directory
for adb retrieval; no other app data is copied. PullReports defaults to
`.artifacts/wi032/samsung-reports`. Airplane-mode cold launch and touch/render correctness
are human-observed device checks, still outstanding.

Run the approved five refresh/five cold offline reopen repetitions for each profile and
twenty search terms, recording failures, all runs, median and worst. Establish the actual
10 Mbit/s/100 ms RTT conditions separately before judging that network target. The 2 Mbit/s
mode is a degraded diagnostic. Stop for Android review after 1B-B. The older iPad mini and
Mac/Xcode/signing compatibility remain the final 1B-C check before 1C.

## Checkpoint 1B-B connection attempt — 20 October 2026

The developer authorized continuation with **proceed**, then reported **Connected and debugging
allowed**. ADB showed only an offline discovered wireless transport, then no devices. Windows
also showed no Samsung/Android USB hardware ID or WPD tablet. No target was selected, installed
or launched. The developer subsequently answered **Cannot connect right now**. Samsung runtime,
correctness and timing evidence therefore remain **not run**; 1B-B is incomplete and WI-032
stays active. No iPad or production work started.

Preparation completed while checking the connection: verified the final APK hash and all three
seed-42 fixture hashes; generated a seed-77 baseline fixture with the unchanged 1A generator for
owner isolation; verified all four owner/profile manifest endpoints on the loopback fixture
host. The temporary host was stopped using its verified PID/start time/command line. Thirty
planned refresh/offline-reopen trials remain marked `not-run` in
`.artifacts/wi032/samsung-session/run-checklist.json`. No application code changed or tests
needed rerunning for this preparation/documentation.

When the Samsung is available, reconnect it to this PC, enable/authorize USB debugging, then
resume explicit-device preflight and the commands above. Do not repeat the build checkpoint.
