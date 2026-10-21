# WI-032 — startup handoff tracing plan

## Selection and approval boundary

Resume WI-032, **Sign in on the tablet and download the day's data offline** (T-4.1.1),
the sole active item in `plan_docs/field-sales-delivery/plan-data.js`. Iteration 1 / phase-1;
Human Tight-Loop implementation; Human-Led tests; Complex; High risk; Low confidence.
Dependencies WI-002, WI-021 and WI-005 are done. The card has no explicit open gate or
checkpoint; production adoption remains a later decision. The agreed `workItemStatus`
map remains authoritative in place of an embedded HTML status block. Keep WI-032 active.

The developer's **proceed** after pushed commit `709f8af` authorizes reviewing the completed
local-open diagnostic and preparing this next bounded increment. This plan and its seven
scenario intents were approved by the subsequent **proceed**. The developer owns interpretation;
the agent executes this one agreed diagnostic increment and stops for review afterward.

## Evidence and question

The [local-open results](tablet-offline-local-open-diagnostic-results.md) contain five original
offline process-cold opens. In the slowest, the worker-body clock is 638.359 ms and the following
worker-end-to-first-continuation-marker interval is 2,582.253 ms, within a 3,250.301 ms enclosing
open. Another open has a 2,401.541 ms continuation interval. All five worker bodies are below
639 ms. Those observations do not justify changing SQLite or identify a runtime/UI fault.

The existing worker-end marker is inside `LocalOpenAttempt.Read`'s `finally`, before the
delegate actually returns. The continuation marker is inside `ObserveUiContinuation`, after
control has entered that helper. Consequently the interval can include worker unwind/task
completion, scheduling, startup and entry into instrumentation. Scheduling or first-use helper
work are hypotheses **(inferred)**. Neither is established by the existing reports.

Resolve: **where does a reproduced delay fall between the worker's end marker, the delegate's
return boundary, the first caller statement after await and the existing continuation marker;
and what are the corresponding threads doing in the Android scheduler trace?** The Blazor
continuation must not be assumed to run on Android's main thread; identify the actual TIDs.

Read-only preflight on 21 October 2026 found emulator-5556 online at API 36, the spike foreground,
Perfetto available, `linux.ftrace` / `linux.process_stats` advertised and no active trace session.
The `sched`, `freq`, `view`, `webview`, `am` and `wm` categories are listed. No trace was recorded,
app launched/stopped/installed, network setting changed or build run during this planning pass.
Recheck identity, allocation, installed APK and capabilities immediately before execution.

## One agent-executed increment after approval

1. **Preserve current outcomes first.** Run the existing 38 tablet tests before modifying source,
   retaining the real-SQLite active-day, edited-work, report-order and failure characterizations.
   Preserve `709f8af`, its APK, five reports and full-suite evidence separately.
2. **Add bounded correlation markers.** Extend the existing diagnostic helper and component with
   an attempt ID and a small set of monotonic timestamps: UI dispatch, worker entry/body end,
   worker-delegate exit, first caller continuation, the existing continuation observation and
   UI completion. Capture caller timestamps before entering additional instrumentation helpers.
   Keep the current read sequence and existing enclosing/stage clock scopes. Add explicit
   interval definitions and a new report revision, `spike-measurements-v4`; the extra probes'
   cost is included where it occurs and the new set is not a controlled comparison with v3.
3. **Join those points to Android traces.** Use a small optional diagnostic hook and
   `DeviceReportAdapter`'s Android implementation to emit short, uniquely named, balanced
   `Android.OS.Trace` begin/end markers on the calling thread. Each marker closes synchronously;
   no synchronous section crosses an await or thread. Record managed/native thread identity
   and whether it is the Android main thread. Bracket hook execution with the same Stopwatch
   clock so trace-event placement and instrumentation delay are explicit. A hook failure is
   diagnostic unavailability and cannot replace a storage exception or change a read outcome.
   Keep Android references out of the portable core; unsupported/headless native fields are null.
   Do not warm up helpers, change await/synchronization behavior or add delays to the app.
4. **Validate and inspect a separate APK.** Run the agreed cases and complete existing .NET/JS
   suites; retain standing warning accounting. Publish a separate Release APK with the installed
   manifest workflow, without workload installs or broad restore. Verify its hash, signature,
   ZIP/ELF alignment and unchanged pinned SQLite assets. Install in place without clearing data;
   verify the retained generation and edited work before and after the update.
5. **Capture exactly three planned offline cold opens.** Use only the allocated
   `WI032_Tablet_API36` / emulator-5556 and retained stress day. No fixture download, data/cache
   reset, AVD restart/resize or extra warm-up launch. Record actual initial network/wake state;
   use airplane mode on, Wi-Fi off, no task reverse route or fixture host. Force-stop only the
   spike between cases, after preserving the preceding report. Start one named, bounded
   Perfetto session before each launch, acknowledge capture readiness, and retain all attempts.
   Use scheduler switches/wakeups, process/thread metadata, frequency and framework
   activity/view/WebView events; enable application annotations only for the spike package.
   Each capture lasts at most 60 seconds, uses a 32 MiB trace buffer and caps its file at 64 MiB.
   Observe readiness with a 45-second cutoff. A cutoff, missing marker, trace error or data loss
   is retained as incomplete evidence, never replaced by another attempt. Stop for review if
   capture cannot provide the required correlation; do not silently switch profiling methods.
6. **Analyze original evidence and hand off.** After each trace ends, retrieve its original file
   and export the original report. After the third trace, run the existing twenty search terms
   once before that report's export. Keep export/search/host-polling clocks separate from the
   traced startup. Use local Trace Processor queries to join attempt IDs, marker names and
   native TIDs, check trace health and summarize running/runnable/waiting intervals for the
   relevant threads. Preserve unknown ranges rather than imputing time. Obtain any needed
   official Perfetto analysis binary only under the ignored task directory, recording its
   origin/version/hash; no system installation or trace upload. No repository builds/tests run
   during capture. Document all three cases, even if none reproduces the long interval.
7. **Finish this increment.** Verify installed APK/data identity, completed trace sessions and
   scoped cleanup. Restore actual initial network/wake settings; remove only this task's
   temporary device trace/config files after verified local copies. Leave the shared emulator,
   other apps and Docker running. Commit and push the completed checkpoint on
   `feature/wi-032-sign-tablet-download-day-s`, then stop for review before a fix or further runs.

Check foreground/concurrent use before UI input. If another task is active, stop automation;
do not repeatedly reclaim the emulator, stop another app or cancel someone else's trace.
After an in-place update, any foreground change must be recorded and assessed against the
existing allocation before the first planned launch; it is not a reason for an extra warm-up.
Preserve an in-flight original report before any package force-stop.

## Expected files

| File | Change |
|---|---|
| `tools/FieldSales.TabletSpike.Core/LocalOpenDiagnostics.cs` | Additional boundary/correlation metadata and optional hook, preserving read outcomes and existing clock meanings. |
| `tools/FieldSales.TabletSpike.Core/SpikeMeasurements.cs` | New diagnostic report revision and explicit availability/scope. |
| `tools/FieldSales.TabletSpike/Components/Pages/Benchmark.razor` | Caller boundary timestamps and trace correlation without changing dispatch/await behavior. |
| `tools/FieldSales.TabletSpike/DeviceReportAdapter.cs` | Android marker/thread metadata; unavailable native tracing on other platforms. |
| `tools/FieldSales.TabletSpike/Platforms/Android/AndroidManifest.xml` | Local profiling flag required for the approved Release app markers; no debuggable flag or production app change. |
| `tests/Integration/FieldSales.TabletSpike/LocalOpenScenarios.cs` | Agreed trace/outcome/failure/export cases. |
| This plan, `docs/tablet-offline-startup-handoff-trace-results.md` (new), device-results index and `plan_docs/.agent-notes/WI-032.md` | Evidence, limits, cleanup and review checkpoint. |

Raw APKs, bounded controller/configuration, traces, SQL queries/results, original exports and
preflight/cleanup records go under ignored `.artifacts/wi032/startup-handoff-traces/`.
No store/schema/package/lock/native-library changes are expected. Preserve the
three pre-existing untracked code-review documents.

Implementation detail verified before native launches: Android 16's application binding permits
app-generated Trace markers only for profileable/debuggable apps. The existing Release manifest
had neither flag. Add `<profileable android:shell="true" />` only to this isolated spike as
necessary tracing plumbing; the APK remains Release and non-debuggable. This extra manifest
file does not change the approved scenarios, read behavior or profiling method. Preserve the
first successful, uninstalled pre-flag build log separately; inspect the final flagged APK.
Sources: [Android profileable manifest element](https://developer.android.com/guide/topics/manifest/profileable-element)
and [Android 16 ActivityThread](https://github.com/aosp-mirror/platform_frameworks_base/blob/android16-release/core/java/android/app/ActivityThread.java).

## Human-Led scenario intents for approval

1. **Unchanged local outcomes:** enabling trace diagnostics retains active generation/manifest,
   edited Unsent items/fingerprint and latest-report ordering; no download, activation or write
   to the original saved reports occurs. Existing characterizations pass before source changes.
2. **Truthful boundaries:** successful UI-dispatched reads export ordered caller/helper points,
   nonnegative intervals and actual thread identities. Hook cost is bounded by measured brackets;
   enclosing clocks are not summed with their subsets or represented as first paint.
3. **Original failure wins:** a real SQLite read failure keeps its original exception, failed
   stage, partial/null measurements and saved work. A failing diagnostic hook records its own
   unavailability without masking that error or turning a successful read into a storage failure.
4. **Stable, partial export:** headless/unobserved native/UI fields remain unavailable rather than
   zero; schema-v4 serialization/save preserves the original captured values. Initial report
   creation still waits for `localReady`, and refresh status remains separate from local reopen.
5. **Three original Android attempts:** each planned offline cold launch retains generation and
   edited work, its own report/trace identity and all cutoff/failure outcomes. The final twenty
   searches match the independent stress-fixture expectations. No optional logical scan runs.
6. **Trace-quality gate:** verify required markers, process/thread joins, session bounds and
   parser/buffer-loss diagnostics before assigning scheduler time. Missing/lost/uncovered data
   remains unknown; a trace with no slow open is reported as non-reproduction. Scheduling state
   alone cannot establish a managed method, JIT, GC or renderer root cause.
7. **Scoped lifecycle:** no other-app operation or unrelated session cancellation; original
   exports survive interruptions, task capture/helpers end, initial network/wake state returns,
   installed APK/data identity matches and the shared emulator stays running.

These scenarios are the acceptance gate for this increment. Run the full solution and existing
JavaScript suites after source changes; do not weaken/skip existing tests. The preceding
2,355 .NET / 34 JS passes at `709f8af` remain baseline evidence until a new run completes.

## Risks, exclusions and missing information

Tracing and first-use marker interoperability perturb startup; no controlled improvement is
claimed. Host load/caches remain uncontrolled, and three cases may not reproduce the delay.
Perfetto availability does not yet prove a loss-free capture. Without usable markers and
thread coverage, the cause stays unresolved. Even complete scheduler data does not supply
managed call stacks. A root-cause hypothesis needs additional evidence before any fix.

Out of scope: SQLite tuning/retention/batching, runtime or WebView upgrades, AOT/interpreter
changes, startup optimization, another matrix, CPU/heap call-stack profiling, physical-device
acceptance, 1C adoption, production sign-in/Sync, MI-62, WI-033, PR creation or main integration.

Missing information required from the developer for this diagnostic: **none**. Exact Samsung/
iPad mini model/OS, physical availability and production adoption remain later inputs; the
approved emulator substitution persists. Trace capture quality and slow-case reproduction are
execution outcomes to report, not facts to assume. The console format adaptation is recorded
above; no console status or card content changes are required for this planning pass.

## Primary guidance checked — 21 October 2026

- Android's [Trace API](https://developer.android.com/reference/android/os/Trace) specifies
  same-thread balancing and short section labels; no newer tracing library is required here.
- Perfetto documents [app annotations](https://perfetto.dev/docs/getting-started/atrace) and
  [scheduler events](https://perfetto.dev/docs/data-sources/cpu-scheduling), including the
  distinction between running, runnable and waiting states.
- [Capture CLI](https://perfetto.dev/docs/reference/perfetto-cli) documents bounded recording
  and readiness acknowledgement. [Local command-line analysis](https://perfetto.dev/docs/getting-started/command-line-analysis)
  and [trace statistics](https://perfetto.dev/docs/analysis/sql-stats) support explicit
  queries and data-quality accounting. No remote analysis service is needed.

Approval boundary: the [console-delivery-next-item skill](C:/Users/colmk/.codex/skills/console-delivery-next-item/SKILL.md)
requires Human-Led scenarios to be agreed “before implementation begins.” This document is
the concrete next increment for that approval, not authorization to start another increment.

## Quality-gate stop and proposed continuation

The implementation has **2,358 .NET / 34 JS passes** and a verified Release APK. The
[checkpoint](tablet-offline-startup-handoff-trace-results.md) records the first original attempt
and its failed trace-quality gate. Its 841.554 ms local read preserved the day/work, and all
seven markers are present, but `config_write_into_file_discard` and
`config_write_into_file_no_flush` were positive. Cases 2/3 and final searches were not run.
Original evidence/errors remain retained; scoped cleanup is complete. WI-032 active; MI-02 open.

**Focused amendment approved** by the developer's **approved** response after `4998512`:
keep the seven scenario intents and unchanged profileable
schema-v4 APK, SHA-256 `8b51486e7130e41e7ba9809e3b2f5b898a2b3d95c4e29b69616fa4d794964ec5`.
Run only remaining original attempts **2 and 3**; case 1 stays incomplete and is never replaced.
Change exactly these capture settings, keeping duration, sizes, categories, cutoff and strict
quality/marker checks unchanged:

```diff
-buffers { size_kb: 32768 fill_policy: DISCARD }
+buffers { size_kb: 32768 fill_policy: RING_BUFFER }
 write_into_file: true
 file_write_period_ms: 1000
+flush_period_ms: 10000
 max_file_size_bytes: 67108864
```

Perfetto's [buffer guidance](https://perfetto.dev/docs/concepts/buffers) supports periodic
flushing, and its [configuration reference](https://perfetto.dev/docs/reference/trace-config-proto)
defines these fields. A 10-second period avoids a high-frequency flush probe. Its overhead
remains part of changed capture conditions. At amendment approval, the correction had not
been capture-validated; its subsequent validation is recorded in the execution checkpoint below.
Keep case 1's qualification separate from subsequent traces; no pooled improvement claim.

On approval, verify unchanged source/APK identity, retained day/work, allocation/foreground,
no active trace, and capture fresh actual network/wake state. No install, download, rebuild,
full-suite repeat or warm-up is needed while source/APK remain unchanged. Preserve original
controllers; create a continuation controller accepting case 1 as a retained failed attempt,
requiring case 2's quality pass before case 3 and refusing to recreate any start manifest.
Proposed `.pbtxt` files remain ignored local artifacts, not sent to the device or executed.

Capture case 2 and inspect quality, markers and clock brackets before case 3. Another quality
error stops continuation. After case 3 alone, run the original twenty terms once and export
its original report. Analyze only qualified interiors with explicit unknown/clock edges and
actual TIDs. Non-reproduction is a reported limit, not a reason for extra launches. Finish
with scoped cleanup, results/notes, commit/push and a review stop. Physical acceptance, 1C,
optimization, production sign-in/Sync, PR/main integration and WI-033 stay outside this
amendment. This approval authorizes original cases 2/3, followed by the agreed review stop.

## Approved amendment execution checkpoint — 21 October 2026

Original cases 2/3 completed on unchanged source/APK `4998512`; case 2 passed before case 3.
Both corrected captures pass strict error/data-loss, marker/TID and clock-bracket checks.
The twenty terms ran once after case 3 and all independent expected counts verify.
Case 1 stays failed-quality and is never replaced or included in qualified scheduler results.
Case 2 reproduced the slow interval: 3,266.259 ms enclosing,
631.518 ms worker and 2,563.652 ms continuation; main-thread
sleep dominates its delegate-exit-to-caller interior, without establishing managed cause.
See [complete results and limitations](tablet-offline-startup-handoff-trace-results.md).
Actual network/wake state is restored, own captures/helper ended and installed APK hash matches.
All seven agreed scenario intents were checked; three original opens are retained, with only
two qualified traces. No source/build/test repeat or installation was needed; baseline remains
2,358 .NET / 34 JS passes. Stop for review before any further increment. WI-032 active; MI-02,
physical Samsung/iPad mini acceptance and 1C adoption remain open.
