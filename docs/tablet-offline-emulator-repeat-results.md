# WI-032 — Repeated Android emulator measurements

20–21 October 2026, Europe/Dublin. This continues the developer-authorized
[emulator increment](tablet-offline-device-spike-plan.md#emulator-amendment--20-october-2026)
after `feba178`. It repeats the agreed refresh, search and offline-open scenarios using the
unchanged Release APK. Physical Samsung/iPad acceptance and runtime/storage adoption remain pending.

## Conditions and method

- Task-owned `WI032_Tablet_API36`, explicit serial `emulator-5556`; Android 16/API 36,
  Google `sdk_gphone64_x86_64`, X64, WebView **133.0.6943.137**. Emulator **37.1.11.0**,
  medium-tablet profile, landscape **2560×1600**, density **320**, four virtual CPUs,
  **4,096 MiB RAM**, 8 GB data partition, headless software graphics `swiftshader_indirect`.
  This is diagnostic evidence for an x86 emulator, without Samsung ARM performance equivalence.
- Release APK **44,956,825 bytes**, SHA-256
  `bfbb91c32f9b106aec73417322659b828e024e277ab0cb30bc813e4fad874c9f`;
  SQLite **3.53.3**, WAL, synchronous **2 (FULL)** in every exported trial.
  A hash-checked copy of this measured APK is retained as
  `.artifacts/wi032/emulator-repeat-session/measured-release.apk` for future comparison.
- Original seed-42/as-of-2026-10-20 fixtures: baseline **5,000 products / 500 Locations**,
  target **25,000 / 500**, stress **100,000 / 2,000**. Plain JSON/gzip bytes respectively
  **16,441,340 / 1,570,674**, **64,337,468 / 6,294,888**, **342,711,599 / 33,229,222**.
  Fixture contents and APK were unchanged throughout this increment.
- Five replacement refresh attempts per profile, baseline then target then stress, followed by five
  cold-process offline opens for that profile. No deliberate app-data or OS-cache clearing.
  The first two stress refreshes preceded the overnight interruption; the final three followed
  a full AVD restart. Their cache/host conditions therefore differ and remain uncontrolled.
  The loopback fixture host and explicit adb reverse route supplied refreshes. Each selected
  **nominal 10 Mbit/s + initial 100 ms application simulation**. This does not establish a
  controlled physical link or 100 ms round-trip latency. Earlier unthrottled single runs used
  different conditions and are not a controlled performance comparison.
- One twenty-term automated search set per refresh attempt; expected counts checked against
  the fixture distribution, including accent/case/apostrophe variants and empty results.
  Stress sets 2/4/5 ran after recovery; set 2 followed the next-day restart, as explained below.
  Before each profile's offline batch, its reverse route was removed, airplane mode enabled
  and Wi-Fi confirmed disabled. Each trial force-stopped only the spike package before `am start -W`.
- OS caches, host background load and scheduling were uncontrolled.
  UI-automation polling and report retrieval ran alongside the benchmark; their overhead was
  not isolated. The Windows host had
  approximately **31.73 GiB total RAM / 8.69 GiB free** at the recorded preflight sample.
  A later observation during stress refresh 4 showed only **2.18 GiB host free RAM**;
  guest data storage still had about **2.87 GiB free**, with **2.13 GiB guest available RAM**.
  These are point samples, without isolation of paging or background-load effects.
  Temporary, bounded process-only keep-awake requests covered execution; power settings were
  unchanged. No full repository test run competed with these measurements.

## Setup persistence correction

Restart preflight found the spike package absent from the previous task AVD. The earlier
handoff's assertion that its installed stress snapshot would survive full AVD shutdown was
unverified and has been withdrawn in the [first-checkpoint results](tablet-offline-device-spike-results.md).
The earlier package force-close/reopen evidence remains valid; no repeat trial had started.

The task AVD configuration was backed up. This increment pins the installed system-image path,
an explicit `measurement-userdata.img`, QCOW2 use and effective 4,096 MiB RAM, and disables
first-boot snapshot reuse. These changes follow the documented
[emulator data-file options](https://developer.android.com/studio/run/emulator-commandline#advanced).
The single cause of the earlier reset was not isolated. The initial AVD's requested RAM was
1,907 MiB, but its launch log raised it to 4,096 MiB; the earlier document now records that fact.

A separate, excluded setup import (`222ac217…`) seeded baseline and the edited Order note.
A complete guest sync, AVD shutdown, verified headless-engine exit and restart then retained
the installed package, a test marker, generation `5ae7bad5348642f688837315fb8c0ef2` and the edited
work fingerprint. One **System UI isn't responding** dialog during setup boot was captured;
**Wait** was selected. This was an Android System UI incident before measured trials, not a
spike application failure. It is retained in the evidence.

## Measurement results

Of **15 replacement refresh attempts**, **13 completed** and **2 remained incomplete**.
Complete reports match fixture hashes/counts, native settings and edited-work fingerprints.
Recovery reports separately confirm the incomplete attempts' activated data and saved work.
All **15 offline cold-process opens**
reopened the final generation for their profile with Wi-Fi disabled and no fixture-host route.
All **300 searches** returned expected counts: 240 immediately after refresh, 60 after recovery.
Controller readiness timeouts are retained below. The incomplete refreshes are not passes.

Each five-value sequence is in attempt order. Times below are internal instrumented refresh
times, not full tap-to-usable UI latency. See the clock/instrumentation limitations below.

| Profile | Refresh attempts 1–5 (seconds) | Complete-run median | Complete-run worst |
|---|---|---:|---:|
| baseline | 12.395, 12.588, 13.197, 8.554, 7.577 | 12.395 s (5 complete) | 13.197 s |
| target | 41.683, 33.936, 37.340, 38.011, 35.624 | 37.340 s (5 complete) | 41.683 s |
| stress | 252.411, 324.672, 216.200, **≥900.000; incomplete**, **≥900.000; incomplete** | 252.411 s (3 complete) | 324.672 s |

**Two incomplete stress attempts reached ≥900 seconds each.** Their final total refresh
times are unknown; the completed-run median/worst above exclude incomplete attempts and are not
five-completion statistics. No successful rerun substitutes for an incomplete attempt.

| Profile | Download median / worst | Import pipeline median / worst |
|---|---:|---:|
| baseline | 1.389 / 1.428 s | 6.881 / 7.787 s |
| target | 5.170 / 5.234 s | 23.503 / 25.421 s |
| stress | 26.830 / 26.927 s | 119.798 / 132.913 s |

Phase summaries include incomplete attempts where their phases were recorded.
Download includes compressed hashing; pipeline includes decompression/parsing/insertion and
projection/index maintenance. These phases do not include every measured refresh operation.

| Profile | Offline local opens 1–5 (milliseconds) | Median | Worst |
|---|---|---:|---:|
| baseline | 3130.0, 3212.5, 3146.8, 3115.6, 3117.1 | 3130.0 ms | 3212.5 ms |
| target | 3158.2, 3032.1, 2760.0, 3052.5, 3064.2 | 3052.5 ms | 3158.2 ms |
| stress | 3102.5, 3328.5, 2972.8, 266.2, 566.8 | 2972.8 ms | 3328.5 ms |

| Profile | Android activity starts 1–5 (milliseconds) | Median / worst |
|---|---|---:|
| baseline | 4730.0, 2986.0, 2448.0, 2387.0, 2488.0 | 2488.0 / 4730.0 ms |
| target | 4172.0, 2486.0, 2222.0, 2301.0, 2813.0 | 2486.0 / 4172.0 ms |
| stress | 4487.0, 3419.0, 3114.0, 1881.0, 3566.0 | 3419.0 / 4487.0 ms |

| Profile | First-observed-ready polling bounds 1–5 (seconds) | Median / worst |
|---|---|---:|
| baseline | 13.648, 11.880, 11.107, 11.007, 11.141 | 11.141 / 13.648 s |
| target | 13.047, 11.005, 10.450, 15.923, 11.427 | 11.427 / 15.923 s |
| stress | 13.647, 12.580, 16.743, 6.719, 11.067 | 12.580 / 16.743 s |

| Profile (100 searches each) | Render median / worst | Query median / worst | Largest sampled disk | End-of-refresh WorkingSet64 range |
|---|---:|---:|---:|---:|
| baseline | 84.9 / 345.4 ms | 1.4 / 29.4 ms | 94.43 MiB | 314.49–337.28 MiB |
| target | 70.0 / 248.0 ms | 1.4 / 16.0 ms | 394.13 MiB | 351.34–362.38 MiB |
| stress | 67.0 / 525.7 ms | 2.9 / 245.9 ms | 1994.26 MiB | 273.92–344.89 MiB |

Local-open times exceeded the provisional three-second review target in baseline **5/5**, target **4/5**, stress **2/5** trials.
These operation clocks alone cannot establish full cold-launch acceptance. Evaluation-target
internal refreshes remained below 60 seconds under the nominal simulation; the required
physical link and both-device evidence are still absent. The 500-Location baseline/target
search sets were below one second on this emulator. Stress is a capacity diagnostic, not an
accepted production volume.

| Profile | Refresh report ID prefixes, in order | Offline-open report ID prefixes, in order |
|---|---|---|
| baseline | `a78b8074`, `fef526de`, `f39239da`, `a9e5355c`, `bb53f0b7` | `b30f7fc6`, `94595c1c`, `4a2dbdc6`, `67c19119`, `c2937aba` |
| target | `63858c43`, `b2b480ec`, `502c014a`, `f0a35a37`, `394cdd95` | `c806998f`, `27f8bafb`, `dae017b8`, `8cbdca7c`, `ff9980d5` |
| stress | `8745c7c3`, `9006ba3b`, `5b70ca66`, `67589f3b`, `ac949f49` | `5de40eaf`, `08b816e7`, `ac7ef799`, `7d14986c`, `b0591b9d` |

### Interrupted controller and recovered measurement

Stress refresh 2 (`9006ba3b…`) exceeded the controller's **360-second UI-readiness wait** at
00:11 on 21 October. The saved refresh report later recovered from the same generation is
**complete**, with **324.672 seconds** internal `totalRefresh`; activation was retained, as
was the edited work. The report clock ends before final report measurement/writing and the
UI's subsequent local reopen, so a complete report and a later readiness timeout can coexist.
This is retained as a slow observation, not relabeled as a failed import or replaced by a
more favorable run. The remaining controller observation window was extended to 900 seconds;
the performance review targets and application were unchanged.

Windows recorded a user-initiated power-off at 00:47 and sleep/resume through 08:53; the old
host/emulator processes were absent when work resumed around 09:54. The controller timeout
preceded that sleep. There is no evidence that overnight sleep caused the slow refresh.
The same explicit data image retained the package, marker and second stress generation
`1b8c5726cb0b45319b9ad890ef637f8f` on recovery.

Its persisted report had zero searches. Twenty searches were therefore run on the recovered
generation in an offline-open report (`427a6aee…`) and kept separately, without rewriting the
original refresh report. Their counts/timings enter the search summary with that recovery
condition; they are not immediate post-refresh observations. Both original complete stress
timings remain in the completed-run summaries. UI export automation initially looked
for the current-report filename prefix rather than the saved-report prefix; the actual
saved export was retrieved and the chooser dismissed. A fixture-host restart also initially
used the wrong argument name and exited; it was corrected and its manifest checked before
further refreshes. Those operator/controller incidents are retained outside app trial outcomes.

Stress refresh 4 (`67589f3b…`) reached the extended **900-second UI-readiness cutoff** while
the screen still displayed **Downloading and validating synthetic data…**; a screenshot
corroborates the accessibility dump. Only the spike package was force-stopped for recovery.
Its persisted report remains **incomplete**, with no `totalRefresh`, no final memory sample
and a null after-refresh work fingerprint. Those missing fields were not filled in or treated
as a pass. The **≥900-second** observation is a lower bound to the controller cutoff, not a
completed refresh time.

That report recorded plain hash/count verification, a **125.427-second import pipeline** and
activation of generation `da99d37472d74baa8b4c82c1d7367c0e`, but no checkpoint/retention completion
phase. The source writes this recovery report immediately after activation, before the old
generation deletion/checkpoint sequence. This narrows the unfinished interval; it does not
separate deletion, checkpoint or later reporting time. The recovered offline app opened that
same complete active generation and showed the edited fingerprint. A separate recovery search
report verifies its counts and saved work; the original incomplete report stays unchanged.

Stress refresh 5 (`ac949f49…`) also reached the **900-second cutoff** and remains **incomplete**.
Its saved report has a **132.913-second import pipeline**, successful plain hash/count verification
and activation, but no checkpoint/retention completion or final refresh duration. Recovery opened
generation `21aba1a9c9d34ca3bcb5dda345e30b6a`. Its recovery searches and fingerprint are checked in
a separate report. Neither incomplete attempt is replaced by another successful refresh.

Both recovery exports subsequently show **exactly two completed generations: active and prior**,
with no other generation entry. Local reopening does not delete generations. This demonstrates
that old-generation deletion had committed before the forced stops; the stale persisted report
does **not** establish that retention itself was still pending. The fourth recovery also shows
no transfer file; the fifth retains its 33,229,222-byte transfer file. These observations narrow
different progress points in the code, but do not isolate checkpoint, subsequent logical scans
and final report writing. Both remain incomplete refresh reports, separately from valid data.

The fourth recovery export exceeded its original 90-second automation file wait and appeared
later. The fifth exceeded the subsequent 240-second wait and was also retrieved later.
Neither Export action was repeated. File-retrieval waits for the remaining offline reports
were extended to 600 seconds; the measured operation clocks and 900-second refresh cutoff
were unchanged. Slow export is another observed limitation of the current diagnostic path.

## Interpreting startup, storage and memory

`databaseAndWorkReopen` measures the elapsed `OpenLocal` operation: native SQLite/workspace
initialization and schema checks, active snapshot and saved-work reads, saved-report listing,
and background-task scheduling/UI continuation. It does not isolate SQLite I/O. Android
`am start -W` measures activity startup and can finish before asynchronous local data is ready.
The additional **first-observed-ready polling bound** runs from the launch command through
the first successful UI inspection that sees the expected generation, fingerprint and enabled
report action. It includes UI-automation overhead and is not exact first paint or measured
tap-to-usable time. These clocks have different scopes and must not be added or subtracted.

Native startup logs include Choreographer/HWUI scheduling messages. These are compatible with
emulator/rendering overhead, but do not establish its cause or separate initialization, disk
and UI delay. The repeat increment preserves the slower observations without changing app
behavior or relaxing the provisional three-second reopen target. A focused startup profile
and actual target-tablet observation remain necessary before accepting that target.

Code inspection also found that the full/default
[`SpikeWorkspace.Measure()`](../tools/FieldSales.TabletSpike.Core/SpikeWorkspace.cs) performs a **scan of
per-generation JSON lengths** for logical payload totals. The periodic `Measure(false)` samples
avoid that scan. Several full scans are inside the refresh
clock but outside the named import phase; final report writing and export add more scans after
that clock. Repeated stress generations increase the scanned data. These are timings of the
instrumented harness, including its diagnostic work. The cost of those scans was not separately
timed, so the difference between total and named phases cannot be attributed wholly to them.
Separating diagnostic scanning/report finalization from transfer/import/UI timing is a focused
follow-up before drawing production performance conclusions.

Largest sampled disk includes allocated database/index pages, retained/staged generations,
WAL/SHM/journal, transfer and Unsent files. Repeated generations and reusable free pages affect
it; it is neither compressed payload size nor a continuously measured high-water mark.
The APK, OS/emulator image, reports and logs are outside this owner-store file total.
WorkingSet64 is an end-of-refresh process sample, not peak RSS or total application memory.
Memory ranges use available completed-refresh samples; incomplete attempts have no final sample.
Disk maxima cover the recorded refresh, offline-open and associated recovery report samples.

## Evidence and verification

Ignored local evidence is under `.artifacts/wi032/emulator-repeat-session/`: `trials.json`,
`reports/`, per-profile controller logs, UI XML, cold-launch/process/memory logs, Wi-Fi checks,
preflight/configuration backups and full-AVD persistence artifacts. `analyse-repeat.cjs`
validates distinct report IDs, fixture hashes/counts, native/build conditions, all search
results, cold-launch state and preserved edited work, and produces `repeat-summary.json`.
The separate setup import is excluded from the repetition statistics. Share choosers were
dismissed after the local export; nothing was sent to another app or person.

No application, test, package or console source changed in this increment. The previous source
checkpoint's **2,343 .NET / 34 JavaScript passes** remain the source-suite evidence, including
its documented unchanged Web-suite rerun. They were not rerun for these measurement/docs changes.
The emulator trials and independent exported-report analysis provide the current verification.

After all thirty records, a supplemental full AVD shutdown/restart retained the installed package,
test marker, final generation `21aba1a9c9d34ca3bcb5dda345e30b6a` and edited-work fingerprint.
The loopback host was stopped and Wi-Fi disabled. This boot again displayed **System UI isn't
responding**; its XML/screenshot and the first failed automation step are retained. After selecting
**Wait**, native UI inspection verified the generation, fingerprint, profile and SQLite settings.
This supplemental OS incident and UI persistence check are outside the thirty timing records.
See `final-persistence-result.json` and `final-avd-restart-ready-after-wait.xml` in the evidence.

The owned host and AVD were stopped after verification; guest sync and actual headless-engine
exit were checked. Airplane mode was restored to 0 and the task reverse-route list was empty.
Bounded process-only keep-awake requests covered measurement/handoff and are cleared at handoff.
The original emulator, IDE and unrelated apps were untouched.

WI-032 stays **active**, MI-02 stays open. Physical Samsung five-run measurements, the older
iPad mini/Mac check and decision 1C remain pending. The emulator substitution does not approve
production sign-in, the final Sync/storage contract or WI-033.
