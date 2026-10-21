# WI-032 — startup handoff tracing checkpoint

## Wakeup analysis — 22 October 2026 (analysis only)

The approved [wakeup analysis plan](tablet-offline-startup-wakeup-analysis-plan.md) read only
the two qualified traces (cases 2/3) with the pinned `trace_processor_shell` v58.2. Nothing ran
on the device, nothing was built or installed, and case 1 was excluded. All 132 retained
evidence files hash identically before and after. Recomputed clock offsets and interval bounds
equal the retained analysis, and every retained thread-state total reproduces within 0.001 ms.

**Finding: during the gap, the main thread is blocked in one frame, waiting for RenderThread to
finish a 2.7–2.9 s WebView draw of the previous frame.** Main is the thread that later runs the
caller continuation, so it could not run it before that frame returned.

| | Case 2 | Case 3 |
|---|---:|---:|
| Certain gap interior (delegate exit → caller) | 2,555.454 ms | 2,699.972 ms |
| Main's `Choreographer#doFrame`, relative to gap start | −230.085 → 2,490.858 ms | −192.846 → 2,681.282 ms |
| … its nested `draw-VRI[MainActivity]` → `postAndWait` | 2,717.295 ms, ends 2,490.648 | 2,872.501 ms, ends 2,681.236 |
| Main's first running time inside the gap | 2,490.623 ms (woken by RenderThread) | 2,681.221 ms (woken by RenderThread) |
| RenderThread's previous-frame draw (`DrawFrames`) | 233058: 2,749.501 ms | 237301: 2,882.150 ms |
| … of which `WebViewFunctor::drawGl` | 2,615.602 ms | 2,739.654 ms |
| RenderThread starts main's frame | 233067 at 2,490.522 ms | 237310 at 2,680.795 ms |
| RenderThread running inside the gap | 176.633 ms | 179.591 ms |
| `Chrome_InProcGp` (WebView in-process GPU thread) running / preempted / runnable / sleeping | 2,341.417 / 26.184 / 0.511 / 187.341 ms | 2,497.832 / 38.746 / 1.026 / 162.368 ms |
| Busy time across 4 vCPUs / capacity | 2,970.7 / 10,221.8 ms | 3,081.5 / 10,799.9 ms |

How the slices fit together:

- Both long frames start about 200 ms before the worker delegate exits, while the storage worker
  is still reading. The four RenderThread draws before each long one took 30–51 ms.
- Android's [`DrawFrameTask::postAndWait`](https://github.com/aosp-mirror/platform_frameworks_base/blob/android16-release/libs/hwui/renderthread/DrawFrameTask.cpp)
  queues the frame's `run()` on RenderThread and blocks the UI thread until `run()` signals.
  RenderThread was still drawing the previous frame. It began main's frame 0.126 / 0.441 ms before
  `postAndWait` ended, and main then ran for the first time in the gap.
- Inside the long draw, RenderThread mostly sleeps in `WebViewFunctor::drawGl`. WebView's
  in-process GPU thread runs for 91.6 / 92.5% of the gap and is rarely kept waiting for a CPU.
  It emits no trace slices, so its internal work is not visible. RenderThread's own
  `shader_compile` / `cache_miss` / `FillRectOp` slices are small (under 30 ms).
- About 70% of the four vCPUs' capacity was idle. The draw is bound by one busy thread, not by
  a saturated system. These are guest-visible figures; host scheduling of the emulator's vCPUs
  is not observable.

**End-of-gap waker (scenario 3).** Case 2: `VizWebView` (TID 25845, app process, process
context) woke main 12.061 ms before the caller timestamp, after a 2.403 ms sleep. Case 3:
`Chrome_IOThread` (TID 26164, app process, process context) woke it 10.750 ms before, after a
0.479 ms sleep. Main then ran 12.003 / 10.193 ms before the caller timestamp. The two wakers
differ. Both are short frame-pipeline handoffs after the long frame. Neither identifies who
queued the continuation, which is not visible.

**Waker chains (scenario 4).** Every hop lies inside the gap and passes through WebView/HWUI
frame threads. Case 2: eight hops alternating `VizWebView` ↔ `RenderThread`; the eighth
(`RenderThread`) was woken by `Chrome_InProcGp` at 2,336.177 ms; stopped at the hop limit.
Case 3: `Chrome_IOThread` ← WebView renderer `Compositor` (sandboxed process; it had slept
1,868.786 ms) ← main ← `VizWebView` ← `RenderThread` ← `VizWebView` ← `RenderThread` ←
`Chrome_InProcGp`. It stopped at hop 8 because the GPU thread had no sleep in its last 400
scheduler states.

**Main-thread activity (scenario 5).** Case 2: 13 running segments, 24.509 ms in total, all at
or after 2,490.623 ms. Seven started from a fresh wakeup: RenderThread 3, `VizWebView` 2,
surfaceflinger `binder:555_3` 1, `Chrome_IOThread` 1. The rest resumed after being switched
out while runnable. After release, main ran frame 233074, whose own `postAndWait` waited about
33 ms for RenderThread, then vsync 233089, then the caller statement. Case 3: 7 segments,
15.483 ms, all at or after 2,681.221 ms. Fresh wakeups: RenderThread, `VizWebView` and
`Chrome_IOThread`, one each. After release it ran animation frame 237317, then the caller
statement. An unclosed `IC.hideRequestFromApi` async slice on the app process spans both gaps.
It has no end in the trace and is not interpreted.

**Established, in both cases (n=2, emulator):** the delegate-exit-to-caller gap coincides with
main sleeping inside one frame's `postAndWait`. That frame was queued behind RenderThread's
2.75–2.88 s draw of the previous frame, almost all of it WebView GL drawing while WebView's
in-process GPU thread was CPU-busy. Main cannot run the continuation until it is released.
After release, a further 64.6 / 18.7 ms of frame work passes before the caller statement runs.

**Not established:** why the GPU thread's work takes so long, since it has no slices. The
emulator's software graphics are a likely contributor **(inferred)**. Also unknown: what the
previous frame drew or what triggered it; when the continuation was queued for main; and
whether a physical tablet with a hardware GPU behaves the same. This is thread-level
correlation, not a managed-method, JIT or GC cause. It makes no improvement, physical-device or
MI-02 claim and proposes no fix. The storage worker body (605–632 ms) is not the long part of
these opens.

Method notes: the first script run treated runnable rows that follow running rows as wakeups.
It was corrected before any interpretation, and its output is kept under
`preliminary-walkback-bug/`. One supplementary read-only query, within step 6's scope, added
the GPU thread's states and the RenderThread/main frame order around the gap. Evidence:
`.artifacts/wi032/startup-handoff-traces/wakeup-analysis/` (`analyse-wakeups.py`,
`summary.json`, `case-2/3-wakeups.json`, every query's SQL/CSV/stderr, `hashes-before.txt` /
`hashes-after.txt`).

## Outcome after the approved continuation

The focused amendment is complete: **all three original offline opens are retained; cases 2/3
pass trace qualification, case 1 remains failed-quality, and all twenty final searches verify**.
Only original cases 2/3 ran on the unchanged schema-v4 APK/source at `4998512`; no replacement,
warm-up, rebuild, reinstall, fixture download, source test repeat or optimization occurred.
The original **2,358 .NET / 34 JavaScript passes** remain the unchanged-source baseline.
WI-032 stays active; MI-02, physical Samsung/iPad mini validation and adoption 1C remain open.

Case 2 reproduced a **3,266.259 ms** local open: **631.518 ms**
worker body and **2,563.652 ms** worker-end-to-existing-continuation interval.
The delegate-exit-to-first-caller boundary accounts for **2,555.466 ms**.
Within that interval's certain clock interior, main TID **25789** records
**2,526.003 ms sleeping**, versus
**24.509 ms running** and
**4.941 ms runnable**.
This narrows the observed gap to a mostly sleeping main thread before the caller timestamp.
It does not identify the awaited work, queue, lock, managed method, JIT, GC or renderer cause.
No managed stacks were captured; the delegate-exit boundary precedes actual Task completion.

Case 3 records **3,358.998 ms** enclosing and **2,700.965 ms**
continuation. The twenty terms ran once after case 3's capture ended; every match count agrees
with the independent 2,000-Location oracle, including case, accents and apostrophe forms.
Edited work, note, stress generation, manifest hashes/counts, owner and SQLite settings verify
in both original reports. No logical payload scan or native peak-memory metric was available.

| Original case | Trace quality | Enclosing ms | Dispatch ms | Worker ms | Continuation ms | Searches |
|---|---|---:|---:|---:|---:|---:|
| 1 | **Failed** | 841.554 | 111.214 | 721.205 | 8.233 | 0 |
| 2 | Pass | 3,266.259 | 69.851 | 631.518 | 2,563.652 | 0 |
| 3 | Pass | 3,358.998 | 47.733 | 605.409 | 2,700.965 | 20 |

Qualified cases **2/3 only (n=2)**: enclosing median/worst
**3,312.629/3,358.998 ms**;
worker **618.464/631.518 ms**;
continuation **2,632.308/2,700.965 ms**.
**2/2** exceed the provisional three-second local-open proxy.
Case 1 is excluded from this qualified summary and has no assigned scheduler time. No pooling
with schema-v3, controlled improvement, first-paint or physical-device acceptance claim.

## Qualified trace correlation and conditions

Cases 2/3 used the approved `RING_BUFFER` and `flush_period_ms: 10000`, with unchanged
60-second duration, 32 MiB buffer, 64 MiB file cap, categories and 45-second readiness cutoff.
Both original traces have **zero positive error/data-loss diagnostics**; all nonzero info
statistics are retained, including initial compact-scheduler skips and flush counters.
Each has exactly seven attempt-specific markers, native PID/TID joins, same-thread sections
inside their measured hooks, ordered raw boundaries and a nonempty clock-offset intersection.
An independent bounds query places all seven markers inside each original trace; observed
trace spans are 59,212.633 / 59,136.581 ms for cases 2/3 under the 60-second configuration.
Case 2 passed before case 3 started. Case 1's original configuration/errors are unchanged.

| Case | Worker-end to delegate-exit ms | Delegate-exit to caller ms | Caller to existing helper ms |
|---|---:|---:|---:|
| 2 | 0.180 | 2,555.466 | 8.006 |
| 3 | 0.195 | 2,699.985 | 0.785 |

The three intervals partition each report's existing continuation clock. Hook/recorder costs
remain where measured; the first dispatch probe itself is intrusive (case 2 recorder entry
54.788 ms plus hook 2.437 ms). These are instrumented observations, not production timings.

Per-thread accounting below uses only the interior guaranteed by all raw/native clock
brackets. Uncertain edges and uncovered time remain unknown. Each thread has its own wall-time
accounting; **do not add the worker and main-thread rows**. `R` is runnable, `R+` runnable
(preempted), and `S` sleeping, as described by [Perfetto's scheduler documentation](https://perfetto.dev/docs/data-sources/cpu-scheduling).
Sleeping state alone does not identify a managed wait or its cause.

| Case | Raw interval | TID | Running ms | R ms | R+ ms | S ms | Unknown / edge ms |
|---|---|---:|---:|---:|---:|---:|---:|
| 2 | workerEndToDelegateExit | 25885 | 0.167 | 0.000 | 0.000 | 0.000 | 0.013 |
| 2 | delegateExitToCaller | 25789 | 24.509 | 2.547 | 2.395 | 2,526.003 | 0.013 |
| 2 | delegateExitToCaller | 25885 | 2.556 | 0.000 | 0.000 | 2,552.898 | 0.013 |
| 2 | callerToExistingHelper | 25789 | 7.903 | 0.090 | 0.000 | 0.000 | 0.013 |
| 3 | workerEndToDelegateExit | 26219 | 0.182 | 0.000 | 0.000 | 0.000 | 0.013 |
| 3 | delegateExitToCaller | 26125 | 15.483 | 1.101 | 1.119 | 2,682.269 | 0.013 |
| 3 | delegateExitToCaller | 26219 | 2.723 | 0.000 | 0.000 | 2,697.249 | 0.013 |
| 3 | callerToExistingHelper | 26125 | 0.772 | 0.000 | 0.000 | 0.000 | 0.013 |

| Case | Main PID/TID | Worker TID | Offset uncertainty ms | Android activity ms | Observed-ready bound ms | Post-input export bound ms |
|---|---:|---:|---:|---:|---:|---:|
| 2 | 25789 | 25885 | 0.012842 | 3,087.000 | 12,224.587 | 781.350 |
| 3 | 26125 | 26219 | 0.012933 | 2,655.000 | 12,052.141 | 721.313 |

The activity, host readiness-polling and post-input export clocks are separate; none is first
paint or added to the enclosing local-open clock. Export excludes input duration. Captures
ended before exports; case 3's search timings are report measurements outside its capture.
Both exports opened the Android chooser and used Back after verifying it; no recipient chosen.

| Case | RunId | AttemptId |
|---|---|---|
| 1 | `1bf6c31b-0951-4351-9104-fa93456e2a5d` | `e0c87ff6-c9e3-4ee4-b88c-75846adbbb4c` |
| 2 | `611a64e0-843a-4c01-8a5e-0160bf2ea147` | `addba8f4-1170-4292-9eac-36820ecd0cdb` |
| 3 | `98bd222f-5a28-47dc-95e2-66f39e578e80` | `eb5254da-0837-4331-82c7-7eb2cddc2358` |

- Case 2: **3,421,542 bytes**, SHA-256 `8775df6b60d1ca1dd933e320513235068b9de687c181136dcba98c19b1320854`.
- Case 3: **3,291,341 bytes**, SHA-256 `acaa75c351ebd325b98d2fcbe41bb20a9bdef324e7bf37e9358d54d9a2aa46ca`.

The installed/archive APK SHA-256 remains
`8b51486e7130e41e7ba9809e3b2f5b898a2b3d95c4e29b69616fa4d794964ec5` (**45,026,457 bytes**).
Device/API/WebView/SQLite and retained stress identities below are unchanged. Fresh continuation
preflight verified saved note `Emulator saved note WI-032`, no active trace/fixture host/task
route, and spike foreground before any input. Fresh host point: **33,271,672 KiB total /
11,730,448 KiB free**. Temporary airplane mode/Wi-Fi-off and wake support were re-established
from the actual initial state. Only the spike process was stopped for each planned cold launch;
OS caches and shared-host background load were uncontrolled.

## Continuation cleanup and review boundary

Fresh actual initial **airplane 0 / Wi-Fi 1 / stay-on 1**, originally awake, are restored.
Own bounded trace sessions ended; the temporary Windows wake helper's PID/start-time identity
and exit verify. Device trace/config/UI files were removed after matching local-copy hashes.
Final installed APK hash matches; tcp:7206 is absent and no fixture host was started.
The shared emulator-5556, other apps and Docker remain running; no reset/resize/restart or
unrelated process/session operation occurred.

Evidence: `.artifacts/wi032/startup-handoff-traces/continuation/summary.json`, fresh lifecycle
records/cleanup proof, `trace-bounds-check.json`, and `emulator/case-2-*` / `case-3-*` with original reports/traces, SQL,
CSV, stderr, configs, launch/UI/export records and completion gates. Prior case-1 controller,
failed analysis, report, trace and cleanup proof remain intact. The separate continuation
controller refuses existing original starts and never creates a case-1 completion marker.

Stop for Human Tight-Loop review of this completed amendment. No extra launches, startup/storage
fix, production sign-in/Sync, MI-62 decision, WI-033, PR or main integration is authorized by it.
Physical Samsung/iPad mini evidence and 1C adoption remain later work.

## Original first-case quality stop (historical)

The [approved plan](tablet-offline-startup-handoff-trace-plan.md) stopped at its required
quality gate after **one of three planned attempts**. Instrumentation is implemented and
**2,358 .NET / 34 JavaScript tests pass**, but the measurement increment is incomplete.
Cases 2/3 and the final twenty searches were not run. WI-032 stays active; MI-02, physical
Samsung/iPad mini validation and adoption decision 1C remain open.

The original report records a successful **841.554 ms** local read, preserving the stress
generation and edited work: **721.205 ms** worker body, then **8.233 ms** to the existing
continuation marker. This one attempt did not reproduce the earlier multi-second interval.
It does not establish an improvement: the APK has additional probes/profileability, and its
trace is unqualified. No scheduler-state duration or managed/JIT/GC/renderer cause is assigned.

| Trace diagnostic | Severity | Value |
|---|---|---:|
| `config_write_into_file_discard` | `data_loss` | 1 |
| `config_write_into_file_no_flush` | `error` | 1 |

The agent-authored capture configuration incorrectly used `DISCARD` with file streaming and
omitted `flush_period_ms`. The validator stopped before another launch. Original configuration,
trace, report, failed analysis and controller error remain retained. These configuration flags
do not establish how many, if any, actual events were lost; they fail the agreed qualification
gate. They are not suppressed to continue. See Perfetto's [diagnostic categories](https://perfetto.dev/docs/analysis/sql-stats)
and [buffer/flush guidance](https://perfetto.dev/docs/concepts/buffers).

Independent marker-only inspection found all seven native markers, matching attempt IDs/TIDs
and a consistent hook-bracket clock offset. This does **not** qualify the trace. The raw
application outcome stays `offline-open-complete`; the separate trace-quality outcome is
failed. Original JSON was not rewritten. Cases 2/3 have no start manifests or replacement runs.

## Implementation and verification

`LocalOpenAttempt` retains storage order/settings/outcomes while adding an attempt ID, seven
boundary observations, actual managed/native thread identity and measured hook brackets.
Caller timestamps precede additional helper entry. Delegate exit precedes its remaining hook
and actual Task completion. UI completion's hook follows the enclosing endpoint. Probe costs
remain included where they occur; enclosing clocks are not added to subsets or called first
paint. Reports use `spike-measurements-v4`. Hook failures cannot replace a storage error or
change a successful read outcome; headless/unobserved UI/native values remain null.
The existing `localReady` gate and separate refresh/local-reopen outcomes remain intact.

The isolated Android manifest adds `<profileable android:shell="true" />` for Release markers;
the APK remains non-debuggable. Android 16 binding source and [manifest guidance](https://developer.android.com/guide/topics/manifest/profileable-element)
were checked before launch. This necessary tracing detail adds one expected-file entry to
the plan. No production app, SQL, store/schema, connection policy, retention, package, lock or
native pin changed.

| Approved scenario | Evidence / status |
|---|---|
| Unchanged local outcomes | Existing 38 tests passed before edits; traced real-SQLite preservation/order cases pass; original native case preserves generation, manifest and edited fingerprint. |
| Truthful boundaries | Portable boundary/serialization checks and seven native marker/TID joins pass. Qualified scheduler accounting for cases 2/3 is recorded above. |
| Original failure wins | Real SQLite failure and throwing-hook cases preserve the original error/work; successful storage reads survive hook failures. |
| Stable, partial export | Null availability and original serialized clocks verified; readiness/refresh separation retained; first original report exported. |
| Three original opens / twenty final searches | **Complete with one unqualified trace:** three original opens retained, cases 2/3 qualified and twenty final search counts verified; no replacement or logical scan. |
| Trace-quality gate | **Stop and amendment enforced:** case 1 remains failed-quality with no scheduling assignment; cases 2/3 pass strict checks and include the reproduced slow case. |
| Scoped lifecycle | Original evidence retained; task trace ended, actual network/wake state restored, helper exited and shared emulator left running. |

Baseline tablet suite **38/38** before edits; local-open cases **8/8**; final tablet suite
**41/41**. Full solution **2,358 passes across nine suites, zero failed/skipped**. JavaScript
**18 Identity / 5 product-form / 11 console**, using current tests/assets and same-lock cached
dependencies. No install/lock edit. Final build: **45 standing NU1902/NU1903 warnings**, zero
errors/new warning classes.

The initial sandboxed build could not resolve the installed Aspire SDK/package source; the
approved-access build passed without project/package edits. Its failed log is retained.
The full-suite logger mistakenly reused `full.trx`: API's TRX was overwritten by DemoData.
API's original 110-pass result remains in `full-tests.log`; the remaining eight original TRX
files were archived as they completed. No tests were rerun to replace the records.
`suite-summary.json` independently verifies all nine console results and retained TRX counts.

## APK and conditions

Final Release APK **45,026,457 bytes**, SHA-256
`8b51486e7130e41e7ba9809e3b2f5b898a2b3d95c4e29b69616fa4d794964ec5`.
Signature v2/v3 and ZIP 16 KiB alignment pass. All **234 packaged 64-bit ELF libraries** have
at least 16 KiB load alignment; all three SQLite assets match pinned **3.53.3** and 16 KiB
alignment. Of 117 legacy 32-bit ELF files, 116 have lower alignment. The first overbroad
all-ABI assertion and corrected inventory are retained; Android's [alignment guidance](https://developer.android.com/guide/practices/page-sizes)
distinguishes 64-bit ABIs. No native binary/runtime changes or 16 KiB-device acceptance claimed.

The successful initial pre-profileable build was never installed and is retained separately.
Only the final inspected APK was installed in place; initial/final installed hashes match.
No data clear, extra warm-up, new download, reset, resize or AVD restart occurred. Installation
surfaced the actual system launcher; existing allocation permitted only the first planned spike
launch, without operating another app's controls. Builds/tests ended before capture.

Device **WI032_Tablet_API36 / emulator-5556**, Android 16/API 36, X64,
WebView **133.0.6943.137**. SQLite **3.53.3 / WAL / synchronous FULL**.
Generation `fb6702ee54a2456c964e7a41dcbe6999`; edited fingerprint
`193a2c1f273b464aecff293c18ca9e2b813936b20a86a5ab2d6b2637e3767075`.
Pre-install UI confirmed **Emulator saved note WI-032**; manifest hashes/counts and original
Unsent hash match. Retained stress day: 100,000 products, 2,000 Locations, 1,440,000 history lines.
Airplane mode on, Wi-Fi off, no task route/fixture host. Host point: **33,271,672 KiB total /
5,621,824 KiB free**. Background load/caches uncontrolled; native peak memory/logical scans
remain unavailable. No physical-device performance claim.

| Original report clock | Milliseconds |
|---|---:|
| Dispatch to worker marker | 111.2141 |
| Worker body | 721.2050 |
| Worker end to existing continuation marker | 8.2327 |
| Enclosing local open | 841.5536 |
| Worker end to delegate-exit boundary | 0.210435 |
| Delegate-exit boundary to first caller timestamp | 7.080673 |
| First caller timestamp to existing helper marker | 0.941609 |

The last three disjoint intervals sum to the continuation interval. Main TID/PID **24935**,
worker TID **25033**. Separate Android activity clock **4,674 ms**, host observed-ready polling
bound **15,911.517 ms**; neither is first paint. Post-input export bound **720.200 ms** includes
polling and excludes input duration.

RunId `1bf6c31b-0951-4351-9104-fa93456e2a5d`; attempt
`e0c87ff6-c9e3-4ee4-b88c-75846adbbb4c`. Original trace **3,160,441 bytes**, SHA-256
`8ad8b9ae1e1a111a1be173097e9b28b3f20e5dd34da48cd8db5a3f2e0d656163`.
Limits: 60 seconds, 32 MiB buffer, 64 MiB file cap. Local Trace Processor **v58.2-add693d8b**
matches its official artifact's SHA-256. No trace upload or system installation.

## First-case cleanup and amendment (historical)

Actual original settings restored: **airplane 0 / Wi-Fi 1 / stay-on-while-plugged-in 1**;
display was originally awake. Task session ended, route absent, fixture host never started,
Windows wake helper exited. Device trace/config/UI files were removed only after local copies
and hashes were verified. Shared emulator, other apps and Docker remain running.

The first cleanup stopped on a timestamp-format comparison: PowerShell had deserialized UTC
as DateTime. Exact UTC ticks and command line proved the original helper identity; corrected
comparison completed cleanup without a process kill. Initial guard log/identity proof remain.
This was not PID reuse or an outstanding cleanup failure.

Evidence: `.artifacts/wi032/startup-handoff-traces/`, especially `suite-summary.json`,
`stopped-case-summary.json`, original APK/logs, `emulator/case-1-*`, original report, marker-only
inspection, quality/controller errors and cleanup records. Two proposed configs remain local,
unexecuted artifacts.

The [focused amendment](tablet-offline-startup-handoff-trace-plan.md#quality-gate-stop-and-proposed-continuation)
proposes `RING_BUFFER` and a 10-second flush for the two remaining original cases on this
unchanged APK. Case 1 remains incomplete and is not replaced. Continuation awaits review;
no optimization, production work, PR/main integration or WI-033 follows automatically.
