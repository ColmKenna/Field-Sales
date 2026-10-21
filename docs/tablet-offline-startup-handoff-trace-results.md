# WI-032 — startup handoff tracing checkpoint

## Outcome

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
| Truthful boundaries | Portable boundary/serialization checks and seven native marker/TID joins pass. Qualified scheduler accounting remains pending. |
| Original failure wins | Real SQLite failure and throwing-hook cases preserve the original error/work; successful storage reads survive hook failures. |
| Stable, partial export | Null availability and original serialized clocks verified; readiness/refresh separation retained; first original report exported. |
| Three original opens / twenty final searches | **Incomplete:** one recorded; cases 2/3 and final searches not run after the quality stop. No replacement or optional logical scan. |
| Trace-quality gate | **Stop enforced:** both configuration diagnostics retained, no scheduling latency assigned. Raw report shows non-reproduction in this one attempt. |
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

## Cleanup and next decision

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
