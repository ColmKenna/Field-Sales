# WI-032 — local-open diagnostic results

## Outcome

The approved [diagnostic increment](tablet-offline-local-open-diagnostic-plan.md) is complete.
All five original process-cold offline opens retained the stress generation and edited work;
the final twenty search terms match expected counts. All **2,355 .NET / 34 JavaScript tests
pass**, with none failed or skipped in the final full run.

The reproduced slow open took **3,250.301 ms**. Its worker body finished in **638.359 ms**;
**2,582.253 ms** then elapsed before the first observed UI-continuation marker. Another open
took **2,973.619 ms**, including **551.125 ms** in the worker and **2,401.541 ms** in that
completion-to-continuation interval. Thus the largest measured contribution in these two
near-three-second observations occurs after the local-read worker's end marker.

This locates an interval, not a renderer/runtime root cause. It includes task completion,
handoff/scheduling and startup or instrumentation entry before the first continuation marker.
It is not isolated UI-thread CPU time or first paint. These observations do not attribute the
old APK's individual outliers, establish a controlled improvement, or justify changing SQLite
storage policy. The instrumented APK and results remain separate from all earlier sets.

## Implementation and verification

`LocalOpenAttempt` extracts the existing worker sequence without changing SQL, connection
settings, ordering, native pins, batching or active/prior retention. It records disjoint
workspace, snapshot initialization/environment, active-read, Unsent-read/fingerprint and report
enumeration/selection stages. Its worker-body clock includes their instrumentation gaps.
Dispatch and continuation clocks use the same monotonic counter as the enclosing operation.

The dispatch interval ends at the first instrumented worker marker and includes setup/startup/
JIT before it; it is not pure thread-pool wait. The enclosing local-open clock still covers
worker dispatch through UI-field updates. Enclosing clocks must not be added to their subsets.
Interrupted stages retain an error/duration while unfinished or unobserved completed-stage
values remain null. Headless reads leave UI-specific clocks unavailable. Exports serialize the
original completed clocks rather than taking replacement measurements.

Reports now identify **`spike-measurements-v3`**. Initial report creation retains the
`localReady` guard, so it waits for the actual read. A failed local read cannot become
`offline-open-complete`; the diagnostic UI also exposes its existing export action when that
read fails. A completed refresh retains its own outcome and attaches the subsequent local-open
diagnostic separately. Production sign-in/Sync and storage behavior were not implemented here.

Verification evidence: `.artifacts/wi032/local-open-diagnostics/`.

| Approved scenario | Evidence |
|---|---|
| Retain active day, edited work and latest-report ordering/selection | Real-SQLite characterization before extraction; retained outcome checks after extraction; all five native generations/fingerprints match pre-install state. |
| Separate worker, stages and continuation clocks | Portable relationship checks and independent checks of all five native reports; completed stages fit worker body, and dispatch/body/continuation fit the enclosing clock. |
| Preserve a real read failure without successful outcome | Native SQLite path-open failure retains its exception/stage, leaves later stages null, reports failed and preserves the original database and Unsent work. |
| Headless/partial unavailability and stable export | Serialization retains null UI/unobserved clocks; saving a completed report retains the original JSON/timings. |
| Readiness gate during initial rendering | Existing `firstRender && localReady` gate retained; all five Android initial reports contain complete measured state. No forced early-render fault injection is claimed. |
| Five offline process-cold opens without replacements | Five unique start manifests, COLD activity launches, original exported reports and matching retained-state checks; no measured case repeated. |
| Final search and scan-independent exports | Twenty expected stress-fixture matches checked independently; every logical-size/scan metric remains unavailable; no optional scan requested. |

Existing tablet baseline: **33/33 passed** before application changes. The two added local-read
characterizations passed against the old inline sequence before extraction. The first fixture
attempt deliberately corrupted a database and retained a cleanup lock: **34/35 passed**. The
fixture was changed to an unopenable database path while preserving the original database;
the next whole characterization run hit an existing HTTP-abort response-header race, again
**34/35**. Its test was unchanged. Both local-read characterizations then passed separately.
All logs/TRX remain retained; the final full tablet suite passed **38/38**, including that HTTP
test. The complete final run has nine suites and **2,355 passes**, with no failures/skips.

The full build has **45 standing NU1902/NU1903 warnings**, no errors or new warning classes.
Direct JavaScript execution first reported missing `jsdom`; current test copies and live source
assets then ran with the cached dependency tree whose package-lock hash matched the source.
All **18 Identity / 5 product-form / 11 console** JavaScript tests passed. No dependency package
or lock changed. Docker Desktop was started for required integration tests; no user containers
were manually stopped or removed. `suite-summary.json` independently checks final results.

## APK and device conditions

Separate Release APK: **44,993,689 bytes**, SHA-256
`353f785b75cea66075a26ce73da36fb4946a7ecb8bea00e4da73c61d4c14b450`.
Signature v2/v3, ZIP 16 KiB alignment, native ELF alignment and all three SQLite asset hashes
match the pinned package. The installed hash matches before measurement and again at handoff.
SQLite remains **3.53.3 / WAL / synchronous FULL**; no runtime/native package changes were made.

Explicit device: **WI032_Tablet_API36 / emulator-5556**, Android 16/API 36, Google
sdk_gphone64_x86_64, X64, WebView **133.0.6943.137**, with the existing retained data image.
No AVD restart, resize, data clear, OS-cache clear, fresh download or extra warm-up launch was
performed by this increment. Case 1 also verifies retained data after the in-place APK update.

Retained generation: `fb6702ee54a2456c964e7a41dcbe6999`.
Edited-work fingerprint: `193a2c1f273b464aecff293c18ca9e2b813936b20a86a5ab2d6b2637e3767075`;
the pre-install UI verified **Emulator saved note WI-032**. All five reports preserve both.
The existing seed-42 stress fixture remains 100,000 products, 2,000 Locations and 1,440,000
historical lines. Native manifest counts/hashes match its existing export. Search expectations
derive independently from its declared six-name/town generation cycle and 2,000 Locations.

Airplane mode was enabled, Wi-Fi disabled and the task's tcp:7206 route absent for all five
opens. No fixture host, repository build or test suite ran during measurement. Each force-stop
targeted only the spike package. Android activity time and host first-observed-ready polling
bounds are separate clocks; the latter include UI-inspection overhead and are not first paint.
The native report's unused LAN selector is not evidence of a connection during these reads;
the shell's offline settings and local-only read sequence establish their conditions.

Host memory point before measurement: **33,271,672 KiB total / 3,359,136 KiB free** (about
31.73 / 3.20 GiB). Host/background load, OS caches and automation overhead remain uncontrolled.
Working-set/peak memory is unavailable for these opens. Export samples physical allocations,
not live payload size; logical totals remain null and no scan clock is synthesized.

## Automation events and cleanup

After the APK update, `global.onsite` was foreground. The initial guard stopped before any
measured launch or UI input. That preflight is retained as `postinstall-foreground-guard.json`;
it is not a sixth attempted open. The existing emulator allocation was used only to launch the
spike for its first planned cold open, without operating the resting app's controls. No extra
warm-up launch occurred. Subsequent foreground guards stayed strict; no further conflict was
observed in the five cases. The precise reason that app surfaced after installation was not
isolated; this is not evidence that its other task was actively issuing inputs.

All five original exports were retrieved and their verified Android share chooser dismissed
only with Back. No recipient was selected. Start manifests, UI/launch captures, original JSON,
export observations and controller progress are retained under `emulator/`. The historical
zero-case preflight error does not represent an outstanding measured-case failure.

Final installed-APK identity and original network state are verified in `final-installed-apk.json`
and `cleanup-result.json`: **airplane mode 0, Wi-Fi 1**, task route absent, no fixture host and
the bounded wake helper exited. The first cleanup wrote its stop marker before reading process
identity, and the helper exited during that lookup; the race is retained in
`cleanup-first-issue.json`. Cleanup was completed with live identity checked before the marker.
No wake-helper process was killed. The shared emulator, other apps and Docker remain running;
no AVD shutdown or unrelated process/container cleanup was performed.

## Review boundary

One of five enclosing clocks exceeds the provisional three-second proxy. The reproduced
outlier and the other near-three-second observation identify a large worker-end-to-continuation
interval, while all measured worker bodies are below 639 ms. Their underlying scheduling,
startup and rendering causes remain open; no UI or database optimization is included.

The current five-case diagnostic and the earlier thirty-case results must remain separate.
Physical Samsung/iPad mini validation and adoption decision 1C are pending; WI-032 stays active
and MI-02 stays open. Stop for review before another increment, optimization, PR/main integration,
production sign-in/Sync or WI-033.

## Native measurements

All values are milliseconds. Five original process-cold offline opens; no warm-up launch or replacement attempt.

| Open | Enclosing local open | Dispatch to first worker marker | Worker body | Worker completion to UI continuation | Android activity | First-observed-ready polling bound |
|---|---:|---:|---:|---:|---:|---:|
| 1 | 651.266 | 21.971 | 625.149 | 3.490 | 4321 | 14671.7 |
| 2 | 3250.301 | 28.369 | 638.359 | 2582.253 | 2952 | 12070.3 |
| 3 | 171.125 | 6.383 | 163.018 | 1.542 | 947 | 5786.9 |
| 4 | 178.647 | 5.442 | 171.598 | 1.437 | 944 | 5657.9 |
| 5 | 2973.619 | 20.311 | 551.125 | 2401.541 | 2705 | 11342.6 |
| median | 651.266 | 20.311 | 551.125 | 3.490 | 2705.000 | 11342.590 |
| worst | 3250.301 | 28.369 | 638.359 | 2582.253 | 4321.000 | 14671.745 |

Worker body encloses the disjoint stages below. Neither enclosing clock is added to its subsets.

| Open | Root/workspace | Snapshot init/environment | Active read | Unsent read/fingerprint | Report enumeration/selection | Unattributed worker-body remainder |
|---|---:|---:|---:|---:|---:|---:|
| 1 | 46.637 | 180.763 | 257.372 | 72.281 | 65.978 | 2.118 |
| 2 | 43.299 | 207.216 | 247.964 | 81.024 | 57.842 | 1.014 |
| 3 | 18.663 | 56.661 | 56.855 | 18.343 | 11.928 | 0.569 |
| 4 | 15.152 | 56.018 | 66.949 | 21.201 | 11.999 | 0.280 |
| 5 | 31.773 | 133.679 | 259.630 | 73.724 | 51.410 | 0.909 |
| median | 31.773 | 133.679 | 247.964 | 72.281 | 51.410 | 0.909 |
| worst | 46.637 | 207.216 | 259.630 | 81.024 | 65.978 | 2.118 |

### Export observations and identities

Export bounds start after the input command returns; include file polling and exclude input duration. These are separate from native local-open clocks.

| Open | Post-command export observation (ms) | Report RunId |
|---|---:|---|
| 1 | 795.143 | `8f270365-eb92-4f95-883b-f5f405ce0140` |
| 2 | 590.458 | `5853a64a-902c-489f-8090-9258028fe530` |
| 3 | 600.055 | `6019c34d-4f61-4cc8-9520-c4a96e29b9e2` |
| 4 | 609.081 | `8fa102d2-18ec-499e-bfbe-14342e2fde01` |
| 5 | 753.256 | `f77ea715-9d70-4cf8-85c5-40e52fde3537` |
