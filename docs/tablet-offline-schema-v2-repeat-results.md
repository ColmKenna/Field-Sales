# WI-032 — schema-v2 repeated emulator measurements

## Scope

The developer's **proceed** after `485e677` continues the previously agreed thirty measurement
scenarios on its unchanged Release APK: five replacement attempts and five offline package
cold opens per baseline/target/stress profile, twenty searches per refresh attempt. App/storage
code, retention policy, SQLite pins and fixtures are unchanged. Physical Samsung/iPad acceptance,
MI-02 and 1C remain pending. This is a separate set from both the old APK's repeat matrix and
the schema-v2 single diagnostic. **All thirty measurements are recorded and independently
verified: fifteen complete refreshes, fifteen offline opens and 300 searches.** Controller
interruptions and their recoveries remain explicit below. No case was replaced by a new
refresh, and physical-device acceptance remains pending.

Evidence: `.artifacts/wi032/schema-v2-repeat-session/`. Archived `measured-release.apk`:
44,638,463 bytes, SHA-256 `8aec18b7143367fa9c517afa66f5c37fca71b62812a9a7ac1562647dff680200`.
Source validation remains the 2,350 .NET / 34 JS passes and Release inspection at `485e677`;
this increment changes only measurement scripts/evidence and documentation.

The session begins with the retained stress generation `b9b4616e15d14459aedb68a19331517c`
and edited-work fingerprint `193a2c1f273b464aecff293c18ca9e2b813936b20a86a5ab2d6b2637e3767075`.
No app data or OS cache is cleared. Initial smaller-profile replacements may retire large
predecessors. Allocated database/free pages can remain large when the incoming day is smaller;
these file-size samples are not live-payload sizes. Record these transitions rather than
resetting the database to produce smaller timings or disk values.

## Method and conditions

Use the same task AVD `WI032_Tablet_API36`, explicit serial `emulator-5556`, Android 16/API 36,
X64, WebView 133.0.6943.137, 2560×1600/density 320, four CPUs, 4096 MiB configured RAM and
headless software graphics, with its explicit persistent 8 GB data image. The installed APK
was pulled and its hash verified. The marker survived restart. One preflight **Android System
UI** ANR was captured and Wait selected before trials; it is outside measurement. Native UI
then verified the incoming stress generation and edited work.

All refreshes select the existing nominal **10 Mbit/s + initial 100 ms application simulation**
over emulator adb reverse. This does not establish physical throughput or RTT. Before each
offline batch, remove the route, enable airplane mode and confirm Wi-Fi disabled. Force-stop
only the spike package before each `am start -W`; this is a cold process with uncontrolled OS
caches. Record activity time and the first observed ready bound, including UI inspection
overhead. Neither is exact first paint. No repository suite or build runs during measurement.
Host background load and automation overhead remain uncontrolled; memory/storage point samples
are retained per profile.

The profile order is baseline, target, stress, with five consecutive replacements followed
by five offline opens for each. This measures replacement into a retained cache, rather than
clean-install initial download. Keep the existing 900-second refresh-observation cutoff and
600-second export-retrieval allowance; controller limits are separate from native clocks.

The host reports 33,271,672 KiB total visible memory (about 31.73 GiB). Free-memory point
samples before baseline/target/stress were 3,647,824 / 1,039,280 / 836,140 KiB (about
3.48 / 0.99 / 0.80 GiB); they do not describe load throughout an attempt. These differing
conditions prevent treating profile or earlier-APK differences as controlled causal comparisons.

The foreground conflict below caused a pause between stress attempts 3 and 4. Scoped host/
wake services stopped at 18:39 UTC; after the developer paused the competing task, the original
spike process/report survived foreground restoration. The same fixture-host DLL restarted at
18:43 UTC before attempt 4, without resetting the AVD/app/data. Host free memory at that point
was 2,725,636 KiB (about 2.60 GiB). This changed-load/service-restart interval remains part of
the recorded sequence and further limits cross-attempt comparisons.

Fixtures keep seed 42 and as-of date 2026-10-20. The target profile is the evaluation scale;
stress explores the capacity boundary.

| Profile | Products | Locations | Historical lines | JSON bytes | Gzip bytes |
|---|---:|---:|---:|---:|---:|
| baseline | 5,000 | 500 | 60,000 | 16,441,340 | 1,570,674 |
| target | 25,000 | 500 | 120,000 | 64,337,468 | 6,294,888 |
| stress | 100,000 | 2,000 | 1,440,000 | 342,711,599 | 33,229,222 |

Original Unsent: 983 bytes, SHA-256
`aca81e6cd733dfb7b5151607efccf441e92074cff6245ce9614f3f0552aacbf3`.
The edited note remains **Emulator saved note WI-032**. Every exported report is checked
against the fixture manifest, native configuration and edited-work fingerprint. Expected search
counts are derived independently from the baseline Location fixture; stress expands the same
generated six-name/town cycle to its declared 2,000 Locations.

Schema-v2 `totalRefresh` ends before final atomic report publication and includes cheap end
sampling/finalization. `refreshAwait` includes publication and the UI continuation;
`refreshAndLocalReopenAwait` also encloses local reopen. The local-open operation includes
initialization/read/scheduling/UI continuation; it is not whole cold-launch time. Deletion,
PASSIVE checkpoint and finalization clocks have separate scopes. Pipeline subsets overlap;
do not sum them. These scope definitions differ from the old APK's enclosing clock, so its
numbers are not pooled with this set. See the [phase diagnostic definitions](tablet-offline-phase-diagnostic-results.md).

Every native sample leaves logical totals/scan clocks unavailable. Export measures physical
file sizes without a full scan. Its file-observation bound starts after the adb tap command
returns, includes polling and excludes the input command's duration; it is not native-write
or tap-to-share latency. Disk includes allocation/free pages and retained/staged data, WAL/SHM,
transfer and Unsent; excludes APK/OS/reports/logs and is not a continuous peak. End working-set
samples are not peak or whole-app memory. Offline-open memory remains unavailable.

Search query timings use native Stopwatch. The visible clock runs from the automated term's
JavaScript invocation through DOM rendering and two animation frames; it excludes a person's
typing. Keep this measured scope when comparing it with the under-one-second search goal.

## Controller incidents retained

Target refresh 2 completed its native refresh and twenty searches and exported report
`9711ec3b-abcf-41a8-bbb8-9d43d9ccf6de`, but the controller did not observe the share chooser.
Its repeated UI captures showed the app, with the Export button partly clipped at the bottom.
The cause of the unobserved chooser is not isolated. The uniquely identified polling controller
was stopped; the original exported JSON was validated and recorded. Neither Download nor Export
was repeated. Native phase/search/work measurements remain the original measurements.

The controller's original input-start timestamp and export-observation stopwatch were not
persisted before interruption; both are unavailable. Its start checkpoint and the native
report's start timestamp are retained under their own names. The incoming fixture/generation
were recovered from the preceding recorded active report. No missing timing is converted to
zero. Export timing medians exclude this unavailable observation while the attempt remains
in refresh/outcome/search statistics. A separate incident JSON, polling-process identity,
logs, UI XML and screenshot remain in the raw folder.

The serial-specific controller now scrolls controls fully above Android's gesture area before
tapping. This changes only automation. The APK, fixture, storage and timed operations are
unchanged. Baseline cold opens 3/4 also had null-root UI-inspection errors which the existing
bounded retry handled; their actual reports and timings remain recorded. The same bounded
UI-inspection retry also handled null-root errors during stress-3 recovery and stress refresh 4;
no measured Download/search was repeated for those inspection errors.

Stress refresh 2 also completed and exported its twenty searches, but the first chooser
capture exposed only the `com.android.intentresolver` root, with no child controls. The
strict surface guard stopped after retrieving the original report
`ae530cf1-649d-45b1-9267-cf7157c2e152`. A subsequent capture verified **Sharing 1 file** and
that exact filename; only Back dismissed it. No Download or Export was repeated. The original
native measurements remain intact; the unpersisted controller input-start and export
observation timing remain unavailable, with its start checkpoint separately named. Its error,
empty-root XML, recovered chooser XML and incident record are retained. The controller now
waits within its existing bound for the expected chooser's children and persists the export
observation before inspecting the chooser. It still stops for other foreground packages and
never selects a recipient. This changes only automation; attempt 3 resumes the existing sequence.

Before any stress attempt, the foreground guard encountered `global.onsite` on the same
verified AVD and stopped without tapping it. The developer explicitly answered **Yes—use
emulator-5556 for the spike**. Only the spike's activity was brought forward; the other app
was not operated or stopped. The aborted preflight had no stress Download/Export attempt and
is recorded separately from the thirty cases. This also confirms shared emulator/background
load was uncontrolled. Leave the AVD running at handoff to preserve that other app's use.

### Repeated foreground conflict — stress attempt 3

After the developer's allocation answer, the other app returned again during attempt 3's
export-file wait. The last pre-tap capture showed a fully visible, enabled spike Export
control; later inspection found `global.onsite` and no new report file. The exact foreground
transition and delivery of that earlier tap cannot be established. No further UI inputs
were sent after detecting the conflict. Only the precisely identified waiting spike
controller (PID 51648) was stopped; session 63891 ended. The other app was not intentionally
operated or stopped. Reliable UI measurement requires the competing task to be paused.

The retained native UI captures show **Refresh complete. All payload counts and hashes
verified.** followed by **Twenty search terms recorded. Export the report.** Active generation:
`debb5a6e44ad42dfb07c8e274e074016`; internal saved refresh:
`report-1bb7c5c7-a7bc-44e0-a781-23bc1dd0a5a0.json`. At interruption, its JSON clocks/searches
were uncollected and excluded from statistics; it was not labelled an application failure
or replaced with another Download. The original evidence is retained, including
`stress-refresh-3-start.json`, completion/search/XML captures, controller identity/stop record
and `stress-refresh-3-uncollected.json`.

Recovery required exclusive use and preservation of the original report. At that interruption,
stress refreshes 4/5 and five offline opens were unstarted. Their later completion does not
erase the interruption or change the earlier APK/diagnostic evidence.

The developer subsequently confirmed **I’ve paused the other emulator task—continue on 5556**.
The spike process (PID 12306) survived, and its original in-memory current report still held
all twenty search observations. One explicitly separate recovery Export retrieved the same
RunId/generation, with the original **200.343-second refresh** and search clocks intact. No
Download or search was repeated. Original export timing stays unavailable; the separate
recovery file-observation bound is **159.074 ms**, excluded from original export statistics.
`stress-refresh-3-recovery-result.json` / `stress-3-recovery-analysis.log` resolve the formerly
uncollected case while retaining the interruption evidence. After the developer paused the
other task and scoped services restarted, the sequence continued at attempt 4 and finished.

## Recorded outcomes

30/30 planned measurements have independently validated exported reports: 15/15 complete refreshes, 0 other exported refresh outcomes and 15/15 offline opens. All 300 exported searches were checked independently.

### Completed refresh operation

| Profile | All values (seconds) | Median | Worst |
|---|---|---:|---:|
| baseline | 49.359, 70.470, 7.103, 8.113, 11.163 | 11.163 | 70.470 |
| target | 40.481, 30.429, 32.845, 31.514, 39.944 | 32.845 | 40.481 |
| stress | 170.919, 216.690, 200.343, 248.178, 205.773 | 205.773 | 248.178 |

### Completed-generation deletion

| Profile | All values (seconds) | Median | Worst |
|---|---|---:|---:|
| baseline | 46.635, 65.371, 0.502, 1.410, 4.196 | 4.196 | 65.371 |
| target | 10.524, 0.799, 2.295, 2.054, 10.615 | 2.295 | 10.615 |
| stress | 48.061, 50.395, 43.270, 92.807, 58.213 | 50.395 | 92.807 |

### Local-open operation

| Profile | All values (ms) | Median | Worst |
|---|---|---:|---:|
| baseline | 3079.6, 3045.6, 450.3, 2831.1, 3187.2 | 3045.6 | 3187.2 |
| target | 3030.2, 3061.6, 3081.6, 3038.0, 2947.6 | 3038.0 | 3081.6 |
| stress | 2938.6, 3071.3, 2934.1, 3044.7, 2896.5 | 2938.6 | 3071.3 |

9/15 recorded local-open operation clocks exceeded the provisional 3,000 ms proxy. This is not a whole cold-launch acceptance result.

### Android activity launch

| Profile | All values (ms) | Median | Worst |
|---|---|---:|---:|
| baseline | 6850, 3034, 3051, 2719, 2875 | 3034 | 6850 |
| target | 4085, 2896, 2956, 2738, 3131 | 2956 | 4085 |
| stress | 6144, 2641, 2586, 2483, 2635 | 2635 | 6144 |

### First observed ready polling bound

| Profile | All values (ms) | Median | Worst |
|---|---|---:|---:|
| baseline | 14853, 11564, 15740, 11194, 11981 | 11981 | 15740 |
| target | 13102, 11618, 11815, 11389, 11937 | 11815 | 13102 |
| stress | 17421, 11284, 11083, 11051, 11049 | 11083 | 17421 |

### Export file observation after input command returns

| Profile | All values (ms) | Median | Worst |
|---|---|---:|---:|
| baseline | 114.4, 114.8, 133.3, 123.9, 143.5, 750.4, 680.9, 725.4, 730.2, 739.9 | 412.2 | 750.4 |
| target | 162.4, unavailable, 126.6, 139.2, 110.3, 724.2, 714.7, 728.7, 710.5, 726.1 | 710.5 | 728.7 |
| stress | 159.0, unavailable, unavailable, 142.0, 122.6, 718.6, 685.1, 740.6, 675.8, 707.8 | 680.4 | 740.6 |

### Search, physical allocation and end samples

| Profile | Query median / worst (ms) | Input-to-visible median / worst (ms) | Largest sampled disk (MiB) | End working-set samples (MiB) |
|---|---:|---:|---:|---|
| baseline | 1.30 / 10.97 | 67.5 / 229.8 | 1969.76 | 304.29, 307.73, 304.61, 284.11, 285.92 |
| target | 1.29 / 15.13 | 75.3 / 293.3 | 1933.79 | 344.87, 348.36, 351.99, 354.28, 358.02 |
| stress | 3.50 / 235.09 | 71.7 / 289.3 | 2001.66 | 300.95, 300.04, 299.98, 269.57, 275.77 |

Search records: baseline 100, target 100, stress 100. Working sets are end samples of completed refreshes only. Export values contain recorded refresh exports followed by recorded offline exports.

3 recorded export-observation timing(s) unavailable; timing medians use only numeric observations. 0/5 completed evaluation refreshes exceeded the proposed 60-second operation proxy; 0/300 verified search observations were at least one second. These emulator/application-simulation observations do not settle physical-link or device acceptance.

### Other completed phase clocks

| Profile | Operation | All recorded values (ms) | Median (ms) | Worst (ms) |
|---|---|---|---:|---:|
| baseline | httpTransferAndCompressedHash | 1374.125, 1387.024, 1408.465, 1390.582, 1395.417 | 1390.582 | 1408.465 |
| baseline | decompressParseInsertAndProjectionIndexes | 1001.325, 3480.495, 4870.843, 4911.146, 5263.649 | 4870.843 | 5263.649 |
| baseline | activation | 2.122, 1.075, 9.278, 7.500, 7.931 | 7.500 | 9.278 |
| baseline | walCheckpoint | 0.095, 0.161, 0.084, 0.092, 0.113 | 0.095 | 0.161 |
| baseline | reportFinalization | 21.239, 10.183, 9.476, 7.937, 16.173 | 10.183 | 21.239 |
| baseline | refreshAwait | 49371.740, 70478.603, 7112.585, 8121.840, 11181.183 | 11181.183 | 70478.603 |
| baseline | refreshAndLocalReopenAwait | 49373.533, 70490.345, 7119.095, 8130.330, 11195.286 | 11195.286 | 70490.345 |
| target | httpTransferAndCompressedHash | 5314.348, 5234.045, 5212.521, 5185.092, 5197.198 | 5212.521 | 5314.348 |
| target | decompressParseInsertAndProjectionIndexes | 23945.762, 24019.012, 25003.053, 23866.833, 23742.789 | 23945.762 | 25003.053 |
| target | activation | 11.836, 6.857, 5.726, 7.957, 6.483 | 6.857 | 11.836 |
| target | walCheckpoint | 0.156, 0.116, 0.115, 0.132, 0.139 | 0.132 | 0.156 |
| target | reportFinalization | 94.167, 13.338, 8.217, 11.302, 9.329 | 11.302 | 94.167 |
| target | refreshAwait | 40528.713, 30443.298, 32855.625, 31527.070, 39953.268 | 32855.625 | 40528.713 |
| target | refreshAndLocalReopenAwait | 40542.885, 30450.441, 32862.170, 31537.492, 39962.664 | 32862.170 | 40542.885 |
| stress | httpTransferAndCompressedHash | 27407.979, 27233.739, 26836.018, 26800.887, 26730.323 | 26836.018 | 27407.979 |
| stress | decompressParseInsertAndProjectionIndexes | 94221.027, 138053.708, 129491.184, 127706.534, 120079.221 | 127706.534 | 138053.708 |
| stress | activation | 12.821, 11.522, 10.468, 8.811, 7.759 | 10.468 | 12.821 |
| stress | walCheckpoint | 0.155, 0.098, 0.059, 0.097, 0.096 | 0.097 | 0.155 |
| stress | reportFinalization | 110.953, 11.646, 1.974, 14.572, 9.260 | 11.646 | 110.953 |
| stress | refreshAwait | 171007.334, 216704.978, 200347.797, 248194.932, 205786.819 | 205786.819 | 248194.932 |
| stress | refreshAndLocalReopenAwait | 171024.629, 216714.570, 200349.568, 248206.884, 205793.340 | 205793.340 | 248206.884 |

### Refresh attempt identities and incoming active fixture

| Profile / attempt | Status | Incoming active fixture | Report RunId |
|---|---|---|---|
| baseline 1 | complete | stress: 100,000 products, 2,000 Locations | `4d8f4da0-54a6-40e1-bd9c-32b61f6bf3a7` |
| baseline 2 | complete | baseline: 5,000 products, 500 Locations | `27818abb-782f-4b4a-9345-6fcbeefedde1` |
| baseline 3 | complete | baseline: 5,000 products, 500 Locations | `3e27fee5-f165-4a5f-9080-9f5a23340a7a` |
| baseline 4 | complete | baseline: 5,000 products, 500 Locations | `c67e2e7d-519f-4b9a-a5c0-081e4660156a` |
| baseline 5 | complete | baseline: 5,000 products, 500 Locations | `8ef29eb7-703a-4b45-9d63-6dbf89553d56` |
| target 1 | complete | baseline: 5,000 products, 500 Locations | `b94b4270-d1ac-4249-9b5a-fccdbe84aa83` |
| target 2 | complete | target: 25,000 products, 500 Locations | `9711ec3b-abcf-41a8-bbb8-9d43d9ccf6de` |
| target 3 | complete | target: 25,000 products, 500 Locations | `71ba4c7e-8d60-4294-bfc7-990a83fb549a` |
| target 4 | complete | target: 25,000 products, 500 Locations | `3bf9cd66-a3d5-49bf-965b-aecd4d07febf` |
| target 5 | complete | target: 25,000 products, 500 Locations | `82913658-ae2c-4fd8-ba3b-4ed63d409ec9` |
| stress 1 | complete | target: 25,000 products, 500 Locations | `069b4271-273c-48d9-ba9a-d337560efaab` |
| stress 2 | complete | stress: 100,000 products, 2,000 Locations | `ae530cf1-649d-45b1-9267-cf7157c2e152` |
| stress 3 | complete | stress: 100,000 products, 2,000 Locations | `1bb7c5c7-a7bc-44e0-a781-23bc1dd0a5a0` |
| stress 4 | complete | stress: 100,000 products, 2,000 Locations | `8783447e-71a2-46ce-8e63-aa7d050ade60` |
| stress 5 | complete | stress: 100,000 products, 2,000 Locations | `f799aa5e-2ff2-43f4-b5c7-8fc193c414ec` |

### Offline report identities

| Profile | Cold-open report RunIds, in order |
|---|---|
| baseline | `d2934277-1ced-4aec-8954-c9e538bfafcb`, `695e3671-21e2-4140-ab61-329ec7493fac`, `c68df282-475f-4ff3-b6a7-8f60b71d3c96`, `cde1ef0f-6e26-497e-b53d-5bc5f31cc5e6`, `7d28d296-a4ef-4c61-9992-5eff5ad55285` |
| target | `862b6308-c751-4918-8034-d9512a3e7019`, `f5efe605-4e6d-495b-9a05-1711edf6d329`, `0e09be3c-838b-490b-afbb-dd94e4f1cfe3`, `a2aef17d-6292-4b5a-b677-01a95ef3da4c`, `a3251207-8e0c-45f0-b19b-c74109b0bdf5` |
| stress | `d3167628-8477-4a52-a7f6-5ec2bb0d7ea0`, `7544b017-15c7-46ea-8a21-23b2de9f1ab1`, `8e44b97a-b379-4337-8ac0-03fccf221ce2`, `632ffde7-aeb6-412c-904b-ee0e75b2b73b`, `e91ba228-aec1-49cc-91a2-2cfebab3451a` |

## Interpretation and handoff

All fifteen exported refreshes have complete native outcomes, verified fixture/configuration
and unchanged edited work. All 300 search match counts were independently checked;
0/300 visible-operation clocks were at least one second. No optional logical scan ran,
and every report retains null/unavailable logical sizes and scan clocks. Neither logical
payload size nor peak memory is established by these samples.

All five evaluation-profile refresh operations are below the proposed 60-second proxy.
9/15 local-open operations exceed the proposed three-second proxy. Data preservation/search
checks therefore do not establish that all performance goals pass. The first two baseline
replacements retired retained stress generations; their large deletion clocks and allocated
free pages belong in any interpretation. Stress deletion and finalization/checkpoint clocks
remain separate in the tables. No controlled old/new APK comparison or cause of earlier-APK
timeouts is established, and no storage-policy change follows from these observations.

Independent full analysis: `final-analysis.log` / `summary.json`; generated tables:
`tables.md`. It checked all thirty unique cases, generation continuity, APK/fixture identity,
native settings, edited work, search terms/counts, phase relationships, unavailable scan
metrics, cold launches/offline conditions and the resolved formerly uncollected attempt.
3 original export-observation timings remain unavailable. Stress 3’s later recovery export
is separately recorded at 159.074 ms and excluded from original-export timing statistics.

Application source is unchanged. The 2,350 .NET / 34 JS checks and Release inspection at
`485e677` remain source evidence; no repository build or suite ran alongside measurement.
A final installed-APK pull again matched the archived 44,638,463-byte Release hash.

Final scoped cleanup completed at 2026-10-21T18:55:26.4447685+00:00: the identified fixture host
stopped, only the task’s tcp:7206 reverse mapping was removed, airplane mode verified 0 and
the bounded wake-helper process verified exited. No emulator UI controller is active. The
shared emulator remains running with app data retained; the other app was not intentionally
operated or stopped. Pre-pause/final lifecycle, APK and cleanup records remain in the raw folder.

This measurement increment stops for human review. Physical Samsung and iPad mini tests,
production sign-in/Sync and adoption decision 1C remain pending; MI-02 is open and WI-032
stays active. No PR/main integration, WI-033 or storage optimization is included.
