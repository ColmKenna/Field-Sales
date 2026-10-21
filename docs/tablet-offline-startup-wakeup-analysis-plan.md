# WI-032 — startup wakeup analysis plan

## Selection and approval boundary

Resume WI-032, **Sign in on the tablet and download the day's data offline** (T-4.1.1),
the sole active item in `plan_docs/field-sales-delivery/plan-data.js`. Iteration 1 / phase-1;
Human Tight-Loop implementation; Human-Led tests; Complex; High risk; Low confidence.
Dependencies WI-002, WI-021 and WI-005 are done. Keep WI-032 active; no console change.

After pushed commit `1469c06` (22 October 2026) the developer chose **option A**: an
analysis-only pass over the two existing qualified startup traces. This plan and its seven
scenario intents need the developer's approval before any analysis query is run. The agent
executes this one increment and stops for review afterward.

## Evidence and question

The [startup handoff results](tablet-offline-startup-handoff-trace-results.md) retain two
qualified schema-v4 traces (original cases 2/3). In each, almost all of the continuation
interval falls between the worker delegate's exit and the first caller timestamp:
2,555.466 / 2,699.985 ms. Within that interval's certain clock interior, main TID 25789/26125
is sleeping for 2,526.003 / 2,682.269 ms, and the worker TID is also sleeping. Main also
records 24.509 / 15.483 ms running, so it ran briefly at least once during the gap without
reaching the caller statement **(inferred from state totals; segment count not yet established)**.

Resolve, per qualified case, inside the certain interior of `delegateExitToCaller`:

1. **Which thread, in which process, made main runnable for the run that executed the
   caller-continuation marker, and was that wakeup from interrupt or process context?**
2. What bounded chain of wakeups led to that waker?
3. What did main do during its brief runs inside the gap (overlapping atrace slices)?
4. Where did CPU time go system-wide (app threads, WebView renderer, system processes)
   versus idle?

Read-only planning probe, 22 October 2026 (whole-trace counts only; no interval query; outputs
in the session scratchpad, nothing written to evidence folders):

- Pinned `trace_processor_shell` v58.2 is used; case-2/3 trace SHA-256 values match the results.
- `thread_state` exposes `waker_utid`, `waker_id` and `irq_context`. Case 2: 59,674 states
  carry a waker and 59,647 raw `sched_waking` events exist; case 3: 52,897 / 52,915.
- Case 2: all 3,248 threads map to one of 408 processes, including `com.fieldsales.tabletspike`,
  the WebView sandboxed renderer, `webview_zygote` and WebView service processes. Four CPUs
  have 36,684 `cpuidle` samples.
- `blocked_function` is unavailable because `sched_blocked_reason` was not captured, so the
  kernel wait site cannot be shown.

## One agent-executed increment after approval

1. **Preflight, read-only.** Hash the pinned processor, case-2/3 traces, original reports,
   `case-*-analysis.json` and existing query CSVs before starting. Case 1 is excluded from every
   query. No adb, emulator, build, install, launch or network command runs.
2. **Reproduce the clocks first.** Recompute each raw interval's certain interior from the
   retained report points with the existing bracket rule. Assert that it matches the retained
   analysis offsets, and that the original thread-state totals reproduce within 0.001 ms,
   before interpreting any new query.
3. **End-of-gap wakeup.** Locate main's running segment that contains the caller-continuation
   marker. Take the wakeup that ended main's last sleep before it (skipping preemption-only
   `R+` rows, which are not wakeups). Report waker TID, thread and process names, `irq_context`
   and timestamp. With no waker row, report **unattributed** and the reason.
4. **Bounded waker chain.** From the waker's thread-state row, follow that thread's own last
   wakeup back, for at most eight hops. Stop on interrupt context, idle/swapper, a missing
   waker, a hop before the interior's start, a cycle or the hop limit, and record the reason.
5. **Main-thread gap activity.** List every main running segment in the interior with its
   start, clipped duration, preceding waker and overlapping slices on main. Reconcile the
   running total with the retained 24.509 / 15.483 ms.
6. **System context.** Report clipped running time per thread for all processes (top 15
   threads, plus per-process totals for the app, WebView processes, `system_server`,
   `surfaceflinger` and others), per-CPU busy versus idle, and the 20 longest slices
   overlapping the interior in the app and WebView processes.
7. **Write only new outputs** (JSON summary plus each query's SQL/CSV/stderr) under a new
   ignored `.artifacts/wi032/startup-handoff-traces/wakeup-analysis/`. Never write to `emulator/`
   or `continuation/`. Final hashes must equal the preflight hashes.
8. **Record and stop.** Add the findings to the results document, an index entry to the device
   results, an execution checkpoint to this plan and the WI-032 notes. Commit, push and stop
   for review. No source changes, so no build or suite rerun; the 2,358 .NET / 34 JS passes at
   `4998512` remain the baseline.

## Expected files

| File | Change |
|---|---|
| `docs/tablet-offline-startup-handoff-trace-results.md` | New wakeup-analysis section: per-case findings, established versus unknown. |
| `docs/tablet-offline-device-spike-results.md` | Short index entry. |
| This plan and `plan_docs/.agent-notes/WI-032.md` | Execution checkpoint, limits and next step. |
| `.artifacts/wi032/startup-handoff-traces/wakeup-analysis/` (ignored) | Analysis script, SQL/CSV/stderr, summary JSON, before/after hashes. |

No application, test, package, console or trace-capture change.

## Human-Led scenario intents for approval

1. **Read-only evidence:** before/after hashes are identical for the traces, reports, prior
   analysis files and pinned processor. Outputs go only to the new folder. No device, build,
   install or launch occurs, and case 1 is excluded.
2. **Clock fidelity:** recomputed offsets and interior bounds equal the retained analysis.
   The original thread-state totals reproduce within 0.001 ms before any new result is
   reported. Uncertain edges stay unassigned.
3. **End-of-gap waker:** for each case, the wakeup that ended main's last sleep before the
   caller-continuation marker is reported with waker TID, thread, process, interrupt or process
   context and timestamp. Otherwise it is **unattributed**, with the reason. It is never
   inferred from proximity.
4. **Bounded chain:** at most eight hops, each stop reason explicit. No conclusion goes beyond
   the last attributed hop.
5. **Main-thread gap activity:** every main running segment in the interior is listed with its
   waker and overlapping slices. The running total reconciles within 0.001 ms. Preemption is
   not reported as a wakeup.
6. **Context, not cause:** per-thread, per-process and per-CPU figures are reported per case.
   Running time summed across four vCPUs is not presented as wall time. Data is guest-visible
   only: host scheduling of the emulator's vCPUs is not observable.
7. **Truthful conclusion:** each case states what is established, whether cases 2/3 agree or
   diverge (n=2), and what remains unknown. A named slice or thread is thread-level
   correlation, not a managed-method, await-target, JIT, GC or renderer root cause. The results
   are emulator-only: no improvement, physical-device or MI-02 claim, and no fix is approved.

These scenarios are the acceptance gate for this increment.

## Risks, exclusions and missing information

An interrupt-context waker (for example a timer) would suggest a timed wait, but not which
code armed it **(inferred)**. The chain may end in binder or `system_server` threads that need
no further interpretation. Compact-scheduler skips recorded near trace start are retained
statistics; the interior's continuous coverage is checked again in step 2.

Out of scope: new captures or launches, source or APK changes, managed-stack capture (option C),
the physical Samsung run (option B), optimization, 1C adoption, production sign-in/Sync, MI-62,
WI-033, PR creation or main integration.

Missing information required from the developer for this analysis: **none**.

## Primary guidance checked — 22 October 2026

- Perfetto's [SQL table reference](https://perfetto.dev/docs/analysis/sql-tables) defines
  `thread_state.waker_utid` ("the thread which caused a wakeup of this thread"), `waker_id`
  (joinable with `thread_state.id`), `irq_context` ("from interrupt context or process
  context") and `blocked_function`.
- Perfetto's [CPU scheduling guide](https://perfetto.dev/docs/data-sources/cpu-scheduling)
  states that `sched_waking` is always emitted on the source CPU and is usually sufficient for
  latency analysis, and defines the S, D, R and R+ states.

## Execution checkpoint — 22 October 2026

Approved by the developer's **Approved** after `98d3c97`. All eight steps ran on cases 2/3
only; all 132 evidence files hash identically before and after, clocks and retained totals
reproduce, and main's running totals reconcile exactly. All seven scenario intents were checked.
Main sleeps through each gap inside one frame's `postAndWait`, queued behind a 2.75–2.88 s
WebView GL draw of the previous frame while WebView's GPU thread is CPU-busy. The end-of-gap
wakers (`VizWebView`, `Chrome_IOThread`) are frame-pipeline handoffs; case 2's chain stopped at
the hop limit and case 3's at a GPU thread with no recent sleep. A first-run walk-back bug was
corrected before interpretation and its output kept separately; one supplementary read-only
query added GPU-thread states and frame order. See the
[results](tablet-offline-startup-handoff-trace-results.md). No source, build, test or device
change. Stop for review before any further increment.
