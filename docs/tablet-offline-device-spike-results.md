# WI-032 — Android storage spike evidence

20 October 2026. The developer approved the [1B plan](tablet-offline-device-spike-plan.md),
including twelve named scenarios, for checkpoint **1B-A only**. This document is the build
handoff, extended with the developer-authorized emulator diagnostics below. Samsung measurements
(1B-B), iPad mini verification (1B-C) and the runtime/storage decision (1C) remain pending.
WI-032 remains active; MI-02 is open.

The subsequent [repeated emulator measurements](tablet-offline-emulator-repeat-results.md)
record the five-run matrix, recovered controller timeout, full-AVD persistence correction and
measurement limitations under separate nominal network-simulation conditions.

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
- Initial Android Release publish: **succeeded with no build warnings**. The 44,617,983-byte
  checkpoint APK is retained as `.artifacts/wi032/emulator-session/original-release.apk`, SHA-256
  `e3e95808229c151cadc77f01a1d936b92f6bb5581fffc3593dc29d65b5bca659`.
  Package/launcher inspection confirms API 24 minimum, API 36 target and ARM64, ARMv7,
  x86_64 ABIs. APK v2/v3 signature and 16 KiB native zip alignment checks pass. The three
  embedded `libe_sqlite3.so` files each exactly match their pinned 3.53.3 NuGet asset.
  Runtime loading was subsequently verified in the emulators below; Samsung remains pending.
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

## Emulator diagnostics — 20 October 2026

The developer instructed **use an emulator for now as I can't get them connected to the same
network**. The [plan amendment](tablet-offline-device-spike-plan.md#emulator-amendment--20-october-2026)
authorizes this interim increment. No discovered wireless device was paired or operated.

### Environments and APK

- Initial AVD `Medium_Phone_API_36.0`, `emulator-5554`: Android 16/API 36, Google
  `sdk_gphone64_x86_64`, X64, WebView 155.0.8059.43, 1080×2400/density 420. Runtime preflight
  loaded SQLite 3.53.3/WAL/FULL. It imported baseline and recovered the previous generation after
  a pre-activation force-close. Its baseline refresh was 6.451 s; twenty searches matched the
  fixture, worst recorded automated render time 122 ms.
- This existing AVD had only 503,016 KiB free before installation and 226,556 KiB after staging
  target. Android refused the corrected APK update with **Requested internal only, but not
  enough space**. This is an installation failure, not an importer SQLITE_FULL result. No other
  app/data was cleared. After reports were retrieved, only this session's newly installed
  `com.fieldsales.tabletspike` and its port mapping were removed from that AVD.
- A separate workspace-owned AVD `WI032_Tablet_API36`, `emulator-5556`, was created from the
  already installed Android 36 Google Play x86_64 image with the `medium_tablet` profile and a
  fresh 8 GB data partition. No existing AVD data was copied/resized/wiped. Android 16/API 36,
  same reported Google model/X64, WebView **133.0.6943.137**, 2560×1600/density 320, about
  1.9 GB requested RAM (the emulator launch log actually raised this to **4,096 MiB**);
  7,005,068 KiB available after install. It ran headless with software
  graphics (`swiftshader_indirect`); it does not represent Samsung ARM performance.
- Corrected Release APK: `.artifacts/wi032/1b-android/com.fieldsales.tabletspike-Signed.apk`,
  **44,956,825 bytes**, SHA-256
  `bfbb91c32f9b106aec73417322659b828e024e277ab0cb30bc813e4fad874c9f`.
  Publish succeeded; v2/v3 signature and 16 KiB zip alignment checks passed. Runtime native
  preflight again loaded 3.53.3/WAL/FULL. Development signing and OS floors are unchanged.

### Reporting correction and functional checks

The original pre-activation restart recovered the old baseline generation and the edited Order
note correctly, but exported an incorrect incomplete startup report (`ce477ae0…`, null manifest/
generation, zero reopen time). Blazor rendered before `OnInitializedAsync`'s local read completed;
`OnAfterRenderAsync` captured partially initialized fields and never refreshed that report.
The component now waits for local initialization before its first report capture. This follows
the documented [asynchronous component lifecycle](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/lifecycle?view=aspnetcore-10.0).
The bad report remains in the evidence and is excluded from successful reopen measurements.
No importer, storage policy, production behavior or tests were changed to accommodate it.

Emulator checks completed:

- Every full baseline/target/stress import verified the original compressed/plain hashes and
  all fourteen counts across the nine payload families. The parser's maximum buffer was
  **65,536 bytes** for each successful tablet run.
- An edited note, **Emulator saved note WI-032**, retained canonical fingerprint
  `193a2c1f273b464aecff293c18ca9e2b813936b20a86a5ab2d6b2637e3767075` across cancellation,
  target/stress refresh, owner switching and cold reopening. The original Unsent fixture SHA
  remained unchanged. Both Calls and the schedule change also remained visible.
- Cancellation at a real partial-download pause kept the old baseline generation and edited
  fingerprint; the report remains **cancelled**, including its deliberate pause time.
- Force-close before activation recovered the previous baseline. Force-close after target
  activation recovered the whole new target generation; its interrupted refresh report
  (`1f083e14…`) remains **incomplete**, separate from the successful reopen report.
- Switching to unused owner 77 showed no downloaded data or owner-42 note; returning to owner
  42 restored its stress generation and edited fingerprint. A second populated owner was not
  downloaded in this emulator increment; that variant already has portable integration coverage.
- Target and stress cold launches worked with test-AVD airplane mode enabled, Wi-Fi confirmed
  disabled and adb reverse removed. Both corrected exports are **offline-open-complete**, with
  the actual manifest/generation, nonzero read time and matching edited fingerprints.
- Twenty fixed search terms per profile (**60 on the isolated tablet, another 20 on the initial
  phone AVD**) returned expected counts, including accent/apostrophe/case variants, partial
  Name/Town matches and zero results. Local report analysis checked those counts against the
  unchanged fixture naming/town distribution and checked manifest hashes/counts/native settings.

### Single-run measurements

These are **emulator diagnostics**, one uninterrupted import per profile. Baseline was fresh;
target and stress replaced existing generations. OS caches and host load were uncontrolled;
repository tests ran concurrently. Transport was **Emulator bridge (diagnostic)** through a
loopback-only fixture host and adb reverse, with no throttling or measured physical link/RTT.
No medians, worst-of-five physical runs or physical acceptance are claimed.

| Profile (run ID prefix) | Total refresh | Largest sampled disk | Last sampled disk | Worst of 20 automated search/render times |
|---|---:|---:|---:|---:|
| Baseline (`22b6d5eb`) | 4.195 s | 35.66 MiB | 29.01 MiB | 257.6 ms |
| Target (`696165d4`) | 13.042 s | 288.60 MiB | 276.56 MiB | 92.0 ms |
| Stress (`f202ca8b`) | 67.177 s | 932.64 MiB | 884.54 MiB | 113.0 ms |

Disk totals include allocated database/index space, retained/staged generations, sidecars,
transfer and Unsent files; they are not payload sizes or continuously measured peaks. These
sequential runs include reuse/retention of earlier generations and database free pages.
End-of-run WorkingSet64 samples were 318.77, 332.12 and 292.28 MiB respectively, **not peak RSS**.

| Offline cold reopen | Database + work read | Android `am start -W` activity time |
|---|---:|---:|
| Target (`24c1bb5f`) | 695.3 ms | 1,663 ms |
| Stress (`96cddd81`) | 3,045.7 ms | 9,693 ms |

The stress read itself exceeded the provisional three-second review target. Android activity
time measures a different scope and also exceeded it; neither clock establishes full
tap-to-usable UI time. Preserve these slower results for physical-device review. Reopen exports
retain the UI's default `LAN` label, but no transfer occurred in these airplane-mode tests.
This increment does not settle MI-02, the supported physical devices or the production budget.

### Verification and handoff

Solution build passed with **0 errors** and only standing NU1902/NU1903 warnings (90). The first
attempt found the owned fixture host locking its output; stopping that verified process and
rerunning resolved the lock. The corrected Android publish used the existing copied-manifest
workaround above, without installing/updating workloads.

All **2,343 distinct .NET cases** pass in the final per-project evidence, with no skips, and
**34 JavaScript checks** pass. The initial full run had 2,342 passes and one Web authentication
redirect after Windows slept from 21:02 until 22:59 local time. System sleep/resume events are
retained. The entire unchanged **766-case Web integration suite** then passed on rerun. A
temporary process-only system-awake request covered completion; no persistent power setting
changed. The two corrected native offline reopens are the runtime regression checks for the
startup-report race; portable tests alone do not exercise that Blazor lifecycle.

Evidence: `.artifacts/wi032/emulator-session/` contains `report-summary.json`, original and
tablet `reports/`, UI XML/screenshots, native/OS preflights, APK metadata/signature log, solution/
Android build logs, `full-tests/*.trx`, `web-rerun/*.trx`, JS logs and sleep events. Interrupted,
cancelled and bad original startup reports remain separate. Transient UI-automation/share-sheet
inspection failures were retried; the final report was retrieved from its local external file
after confirming the share chooser. Nothing was shared to another app or person.

The synthetic host and task-created AVD were stopped after retrieval. The original handoff
assumed the installed app/stress data would persist in `.artifacts/wi032/emulator-avds/`;
the subsequent restart preflight found the package absent, so that whole-AVD persistence claim
was unverified and is withdrawn. Package-only force-close/reopen evidence above is unchanged.
The repeat increment pins a separate explicit user-data image and verifies full AVD restart
persistence before its measurements, following the documented [emulator data-file options](https://developer.android.com/studio/run/emulator-commandline#advanced).
Airplane mode was restored and task port mappings removed. At that checkpoint the original
emulator was left running with its other apps/data intact. To reuse the isolated AVD with the installed emulator:

```powershell
$env:ANDROID_AVD_HOME = 'D:/repos/Field-Sales/.artifacts/wi032/emulator-avds'
$env:ANDROID_HOME = 'C:/Program Files (x86)/Android/android-sdk'
$spikeData = 'D:/repos/Field-Sales/.artifacts/wi032/emulator-avds/WI032_Tablet_API36.avd/measurement-userdata.img'
$spikeSystem = 'C:/Program Files (x86)/Android/android-sdk/system-images/android-36/google_apis_playstore/x86_64'
$spikeArguments = @('-avd', 'WI032_Tablet_API36', '-port', '5556', '-data', "`"$spikeData`"", '-sysdir', "`"$spikeSystem`"", '-memory', '4096', '-no-audio', '-no-snapshot', '-gpu', 'swiftshader_indirect')
Start-Process 'C:/Users/colmk/AppData/Local/Android/Sdk/emulator/emulator.exe' -WindowStyle Hidden -ArgumentList $spikeArguments
# After boot, start the synthetic host (this terminal stays occupied):
pwsh -File scripts/tablet-spike.ps1 -Action Host
# In a second terminal, use the explicit test serial:
pwsh -File scripts/tablet-spike.ps1 -Action Reverse -DeviceSerial emulator-5556
pwsh -File scripts/tablet-spike.ps1 -Action Launch -DeviceSerial emulator-5556 -Cold -ReportDirectory .artifacts/wi032/emulator-reuse
```

Stop for review of this emulator increment. Samsung/iPad measurements and the physical five-run
matrix remain pending; WI-032 stays active. No iPad, 1C, production sign-in or WI-033 work started.

## Reporting diagnostic — 2026-10-21

The subsequently approved reporting increment uses a separate schema-v2 APK and evidence set.
It removes logical scans from timed refresh/export paths, persists pending operation names and
separates deletion, checkpoint and report-finalization clocks. All 2,350 .NET / 34 JavaScript
tests pass. One focused stress refresh completed in 141.210 seconds, including 66.868 seconds
for generation deletion, with successful report exports and offline recovery; all forty searches
and edited work verify. This single diagnostic does not supersede the prior matrix's failures or
establish physical acceptance. See [the phase diagnostic results](tablet-offline-phase-diagnostic-results.md)
for clock scopes, retained failure evidence, APK hash, conditions, cleanup and the review boundary.

## Schema-v2 repetitions — 2026-10-21

The [separate repeated-results document](tablet-offline-schema-v2-repeat-results.md) records
all thirty measurements and 300 independently verified searches on the unchanged schema-v2
APK. All fifteen refreshes completed and all fifteen offline opens retained edited work.
Refresh median/worst: baseline 11.163/70.470 s, target 32.845/40.481 s,
stress 205.773/248.178 s. 9/15 local-open operations exceed the provisional three-second
proxy. Shared-emulator/controller interruptions and original unavailable clocks remain explicit;
stress 3’s original in-memory report was recovered after the developer paused the other task.
No Download/search was repeated to replace a case. Scoped services stopped; the shared AVD
remains running. Earlier failures remain recorded; physical-device acceptance and 1C are
pending. WI-032 stays active. Stop for review before another increment.
