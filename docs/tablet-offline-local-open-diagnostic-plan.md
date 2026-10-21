# WI-032 — local-open diagnostic increment

## Selection and approval boundary

Resume WI-032, the sole active item in `plan_docs/field-sales-delivery/plan-data.js`.
Iteration 1 / phase-1; Human Tight-Loop; Human-Led tests; Complex, high risk, low confidence.
Dependencies WI-002, WI-021 and WI-005 are done. The agreed top-level `workItemStatus` map
remains authoritative; WI-032 stays active. No other work item is selected.

The developer's **proceed** after `991c5ea` authorizes reviewing the completed measurements
and preparing the next increment. The subsequent **proceed** approves this instrumentation
increment and all seven Human-Led scenario intents below.
The previous thirty-case matrix is complete and must not be restarted.

## Evidence and question to resolve

[Schema-v2 repeated results](tablet-offline-schema-v2-repeat-results.md) contain fifteen
successful offline process-cold opens. Their local-open medians are 3,045.6 / 3,038.0 /
2,938.6 ms for baseline / target / stress; nine observations exceed the provisional 3,000 ms
proxy. The individual baseline values range from 450.3 to 3,187.2 ms. No case lost edited work.

`Benchmark.razor:OpenLocal` starts a Stopwatch before `Task.Run`, performs the local reads on
that worker, then stops it after returning to the Blazor continuation and updating UI fields.
The reports preserve only this enclosing clock. Its worker performs these operations:

1. Resolve `Device.Root` and construct the owner workspace.
2. Construct `SnapshotSqliteStore`: open SQLite, apply existing connection/schema setup and
   inspect its native environment.
3. Read the active generation, including another snapshot connection/schema setup.
4. Read Unsent work, including its separate connection/schema setup and canonical fingerprint.
5. Enumerate and sort saved report filenames by modification time.

Existing native logs include skipped-frame messages but have no matching per-stage monotonic
timestamps. They cannot assign the enclosing delay to database work or UI scheduling. A
startup/scheduling contribution is a hypothesis **(inferred)**, not an established cause.
Neither data-size independence nor those logs prove that SQLite is fast or slow.

Resolve: **which completed local-open stage, worker dispatch or UI continuation accounts
for the observed enclosing duration?** Preserve unavailable observations if it cannot be isolated.

## One agent-executed increment after approval

The developer owns scenario approval and interpretation. The agent performs these substeps,
then stops committed/pushed for review; no optimization follows automatically.

1. **Characterize outcomes before refactoring.** Run the existing tablet suite and add the
   agreed real-SQLite local-read characterization for active-generation and edited-work
   preservation. Preserve original outcomes and errors before moving the worker read sequence.
2. **Add diagnostic clocks.** Extract the existing worker read sequence into a portable helper
   without changing operation order, SQL, connection settings, report-list ordering or retained
   data. Record disjoint stages with one monotonic Stopwatch origin: dispatch-to-worker-entry,
   the five operations above, worker completion, and completion-to-UI-continuation. Keep a
   separately labelled worker-body clock and the existing enclosing `databaseAndWorkReopen`
   scope. Nested/enclosing clocks must not be summed. Record unmeasured phases as null, not zero;
   preserve a failed stage without publishing a successful local-open outcome.
3. **Revise diagnostic reports only.** Give the instrumented report a new schema revision;
   retain the existing fields and explicit clock scopes. Keep initial local-ready report
   creation after the actual read. Do not add waits, UI warm-up, new product flows or per-stage
   disk scans. Export must preserve the original clocks. Headless reports cannot claim UI clocks.
4. **Validate and publish.** Run the agreed cases and complete .NET/JS suites with the established
   bounded test setup. Publish and inspect a separate Release APK with the installed-manifest
   build workflow; retain its hash, signature, alignment and pinned native-library checks.
   Do not perform a workload install or broad restore. Keep the prior APKs/reports unchanged.
5. **Run five focused offline process-cold opens.** Use the currently retained stress generation
   on the same verified `WI032_Tablet_API36` / emulator-5556, without a new download, data/cache
   clear, resize or whole-AVD restart. Verify the active generation and edited-work fingerprint
   before installation and afterward. Keep airplane mode on, Wi-Fi disabled and no task reverse
   route/fixture host. After the fifth open, run the existing twenty search terms once and check
   their counts. Export each original report and record all attempts, errors and unavailable
   timings. Capture host conditions, Android activity launch and first-observed-ready polling
   bounds separately from the local-open clocks. Avoid repository builds/tests during measurement.
6. **Document and hand off.** Record every stage/enclosing value, median/worst, conditions,
   uncertainties and any report recovery in a separate results document. Do not pool the new
   instrumented APK with the schema-v2 matrix or claim a performance improvement. Verify the
   installed APK identity at handoff; clear only task network/wake resources and leave the
   shared emulator running. Commit/push on the existing branch and stop for human review.

The existing authorization to use emulator-5556 persists. Check its identity and foreground
before UI work. If another task uses it again, stop automated inputs and resolve concurrent
use; do not operate that task's app or keep reclaiming its foreground. Do not force-stop the
spike before preserving any in-flight original report. Only its package is force-stopped for
the five agreed cold opens.

## Files expected to change

| File | Purpose |
|---|---|
| `tools/FieldSales.TabletSpike.Core/LocalOpenDiagnostics.cs` (new helper/types) | Existing local reads plus measured stages and explicit completion/error state. |
| `tools/FieldSales.TabletSpike.Core/SpikeMeasurements.cs` | New diagnostic revision and optional local-open measurements with clock scopes. |
| `tools/FieldSales.TabletSpike/Components/Pages/Benchmark.razor` | Measure worker dispatch/continuation, use the helper and retain the local-ready gate. |
| `tests/Integration/FieldSales.TabletSpike/` | Agreed native-SQLite outcome and diagnostic-report cases. |
| `docs/tablet-offline-local-open-diagnostic-results.md` (new), this plan and `plan_docs/.agent-notes/WI-032.md` | Results, conditions and current/next checkpoint state. |

Existing store/configuration files need not change to collect these clocks. Any later discovery
requiring behavior or storage-policy changes needs a separate plan amendment.

## Approved Human-Led scenarios

1. A complete local read retains the active generation, manifest, edited Unsent items and
   fingerprint; it selects the same most recent saved report and never downloads or activates data.
2. Successful instrumentation distinguishes worker dispatch, the five read stages and UI
   continuation. Completed disjoint stages fit their enclosing clocks, allowing measured
   instrumentation overhead. Tests assert relationships/outcomes, not wall-clock speed limits.
3. A real local-read failure retains its error/stage and completed measurements, leaves
   unobserved stages unavailable, and cannot produce `offline-open-complete` or delete saved work.
4. A headless/partial measurement leaves UI-specific clocks unavailable. Serialization/export
   retains that distinction and the original completed clocks; missing timings never become zero.
5. Initial Blazor renders while the worker is pending cannot publish a completed startup report.
   Retain the existing `localReady` guard; verify final report completeness in the Android runs.
6. Five offline process-cold opens on the retained stress day each keep the same active generation
   and edited-work fingerprint and export their original diagnostic report. All attempts count;
   an interruption is retained rather than replaced with a favorable rerun.
7. The final existing twenty-term search check matches expected stress-fixture counts. No optional
   logical scan runs, and exports do not replace measured native work with export-time values.

Run the existing full suite after source changes. The preceding 2,350 .NET / 34 JS passes at
`485e677` and unchanged-APK results at `991c5ea` are baseline evidence, not new-run results.

## Limits and missing information

Instrumentation overhead and uncontrolled emulator/host load remain risks; the five runs may
not reproduce every prior outlier. An unattributed delay remains unattributed, rather than
being called a database or UI fault. No elapsed-time target is relaxed to make a case pass.

Out of scope: generation-deletion optimization, indexing/batching/retention/checkpoint changes,
native package/runtime upgrades, another thirty-case matrix, physical-device acceptance, final
storage adoption 1C, production sign-in/Sync, MI-62, WI-033, PR creation or main integration.

Missing information for this diagnostic: none. Exact Samsung/iPad models, OS and physical
availability remain inputs to later gates; the approved emulator substitution still applies.
Preparing this plan made no source/APK/emulator change. The subsequent proceed approved
implementation of this single diagnostic increment; all original evidence stays separate.

## Executed checkpoint

The [separate diagnostic results](tablet-offline-local-open-diagnostic-results.md) record
2,355 .NET / 34 JS passes, the inspected schema-v3 Release APK, five original offline cold
opens and twenty verified final searches. The reproduced slow observation is dominated by
the worker-end-to-first-UI-continuation interval; its underlying cause remains unproven.
Original generations/edited work survived. Scoped cleanup restored the actual initial
network state and left the shared emulator running. Stop committed/pushed for review before
further tracing or optimization; physical acceptance and 1C remain pending.
