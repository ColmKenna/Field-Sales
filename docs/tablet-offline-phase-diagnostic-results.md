# WI-032 — reporting phase diagnostic

## Scope and approval

The developer's **proceed** accepted the next diagnostic increment in
`tablet-offline-device-spike-plan.md` after checkpoint `0702f6b`. WI-032 stays active and MI-02
stays open. This increment changes the synthetic runner's measurement paths, then runs one
focused stress replacement and offline recovery. It does not repeat the thirty-run matrix,
change retention policy, select production storage, or establish physical-tablet acceptance.

The previous APK and all favorable/unfavorable reports remain in
`.artifacts/wi032/emulator-repeat-session/`. Its archived `measured-release.apk` has SHA-256
`bfbb91c32f9b106aec73417322659b828e024e277ab0cb30bc813e4fad874c9f`.
New evidence belongs to `.artifacts/wi032/phase-diagnostics/` and is not pooled with those runs.

## Reporting changes

- Schema `spike-measurements-v2` samples physical database, WAL, journal, SHM, transfer and
  Unsent file sizes without opening SQLite. Those are sampled file allocations, not a continuous
  peak or live-row footprint. APK/OS/report files remain excluded.
- Logical JSON totals are explicitly optional. Unrequested totals and their scan clock are
  **null/unavailable**, not zero or an empty measured dictionary. The separate optional scan
  clock includes opening, querying and closing its connection. The mobile Export action does
  not request this scan. A separately labeled button permits it after a run.
- Reports persist a pending operation before expensive work. Generation deletion,
  `walCheckpoint` and `reportFinalization` have separate completed clocks. Caught unfinished
  attempts use `interruptedPhaseMs` and retain `failedOperation`. A saved pending name identifies
  the last checkpoint, not proof that the operation was still executing when the process stopped.
  The checkpoint clock measures the unchanged PASSIVE PRAGMA call; its return does not prove
  every WAL frame was checkpointed or the WAL file was truncated.
- Same-directory temporary-file replacement preserves the previous valid report during a
  report write. A successful refresh is published only by the final replacement. The prior
  checkpoint remains incomplete with `reportPublication` pending if that publication is stopped.
- `totalRefresh` includes preparation, cheap physical sampling, intermediate report writes and
  end-sample finalization; it ends before the final atomic publication. `reportFinalization`
  measures end samples and Unsent read, excluding its pending checkpoint/publication. UI-exported
  `refreshAwait` also encloses publication and the UI continuation; `refreshAndLocalReopenAwait`
  additionally encloses local reopen. None is a first-paint clock. Pending-boundary persistence
  and deliberate pause are outside individual operation clocks but inside the enclosing refresh;
  other hooks within operations are included. Pipeline subsets overlap their enclosing clock.
- Local-open labeling now includes initialization, schema/active/work/report-list reads, worker
  scheduling and UI continuation. The `localReady` startup guard remains. Export preserves the
  runner's end-of-refresh working-set sample rather than replacing it with a later export sample;
  offline-open reports without a sample say unavailable.

Atomic activation, owner isolation, active + prior completed retention, batch size 128,
SQLite 3.53.3/WAL/FULL, fixture hashes/counts and twenty search terms are unchanged.

## Verification

Before source changes, all **26 existing portable cases** passed. Two additional characterization
tests passed against the old runner: persisted success/failure plus edited Unsent preservation,
and physical totals excluding logical payload bytes.

After the change, **33 portable cases** passed, including the original cases, those two
characterizations, export serialization with an exclusively locked database, and four real
owned-child process-stop cases. The child was stopped with generation deletion, checkpoint,
report finalization or publication pending. Each left a valid incomplete report, the entire new
active generation and edited work; reopening did not rewrite the refresh outcome. Logical
scanning is still verified explicitly; it is absent from timed refresh samples.

The final unchanged full rerun passed **2,350 .NET tests across nine projects**, with no failures
or skips: API integration 110, DemoData 3, Identity integration 794, generator 22, tablet 33,
Web integration 766, API unit 180, Identity unit 258 and Web unit 184. All **34 JavaScript tests**
also passed (18 Identity, 5 product form, 11 delivery console); all fifteen scratch source/test/
package files match the repository. The non-incremental build passed with only the standing
NU1902/NU1903 codes (79 audit warnings). Evidence: `suite-summary.json`, build/test logs and TRX.

The first full-suite attempt is retained in `full-tests.log`: API 110 and DemoData 3 passed;
Identity had 32 Docker setup errors (762 passed), and the tablet suite had two SQLite error 13
failures while seeding the cancellation fixtures (31 passed). C: was then observed at **zero
free bytes**. The Web suite started but the interrupted run has no final outcome. These failures
are not successful validation. No tests were changed or disabled. On continuation, C: had
36,736,483,328 bytes free without further agent cleanup. The entire unchanged suite was restarted
with process-local TEMP/TMP on D:, with the emulator stopped; evidence goes to
`full-tests-resume.log` and `tests-resume/`.

## Release artifact

Published through `scripts/tablet-spike.ps1 -Action Build -ManifestRoot
.artifacts/wi032/installed-manifests`, using the existing SDK 10.0.401 / installed workload
snapshot and D: build temp, without a workload install. The new archived `diagnostic-release.apk`
is **44,638,463 bytes**, SHA-256
`8aec18b7143367fa9c517afa66f5c37fca71b62812a9a7ac1562647dff680200`.
It is the same `com.fieldsales.tabletspike` package (version 1 / 1.0), min API 24 / target 36.
APK v2/v3 signature verification and 16 KiB ZIP alignment pass. Its x86_64, arm64-v8a and
armeabi-v7a SQLite libraries byte-match the pinned 3.53.3 package, each with 16 KiB ELF LOAD
alignment. See `apk.json`, `apk-signature.log`, `apk-alignment.log` and `apk-native.json`.

The APK update preserved the known marker and old stress generation. The initial installation
attempt occurred before Android finished booting and was rejected; retry after confirmed boot
succeeded. One preflight **Android System UI** ANR dialog was recorded and its observed Wait
button selected. It is outside measurement and is not relabeled as a spike-app ANR. These logs,
XML and screenshot remain under `emulator/`; no refresh was started in that preflight.

## Focused native diagnostic

Exactly **one** replacement attempt ran after all tests finished, followed by a package
force-close and offline reopen. No full matrix or optional large logical scan ran here.
The installed APK was pulled back and its SHA-256 verified before the attempt. Device conditions
remain the isolated `WI032_Tablet_API36` / `emulator-5556`: Android 16/API 36, X64,
WebView 133.0.6943.137, 2560×1600/density 320, four CPUs, 4096 MiB configured RAM, software
graphics, explicit persistent data image and no snapshots. No further System UI dialog was
observed during this attempt. Host load/OS caches remain uncontrolled; available host memory
was only **1.12 GiB** out of 31.73 GiB at the start. This is diagnostic evidence, not a physical
tablet budget or a controlled comparison with the old APK.

The unchanged seed-42 stress fixture has 100,000 products, 2,000 Locations and 1.44 million
historical lines: 342,711,599 JSON bytes / 33,229,222 gzip bytes. Transfer used **10 Mbit/s +
initial 100 ms application simulation** over emulator adb reverse, without a physical link claim.

| Clock | Single observed value |
|---|---:|
| HTTP transfer + compressed hash | 26,799.075 ms |
| Decompress/parse/insert/projection pipeline | 47,110.570 ms |
| Atomic activation | 6.000 ms |
| Completed-generation deletion | 66,868.490 ms |
| PASSIVE checkpoint call | 0.111 ms |
| Report finalization preparation | 23.462 ms |
| Refresh through final-publication checkpoint (`totalRefresh`) | 141,209.797 ms |
| Runner await including publication/UI continuation | 141,228.812 ms |
| Runner await + local reopen | 141,234.826 ms |
| Offline local-open operation after package force-close | 289.670 ms |

The controller first observed the completed UI after **143,728 ms** from the return of the
tap command; that bound includes UI inspection overhead and is not first paint. Refresh status
is **complete** in both saved and exported reports. Separate clocks now show deletion consuming
66.9 seconds in this attempt; checkpoint/finalization were small. This does **not** identify
where the two old 900-second attempts stopped, or justify changing the retention algorithm.

Both current exports succeeded without a logical scan. After the tap command returned, the
first file observations were **35.6 ms** for the refresh report and **354.6 ms** for the offline
report. These include adb polling and exclude the input command's duration; they are not isolated
native-write or tap-to-share latency measurements. Logical totals and scan clocks are null /
unavailable in every sample. The native report controls were visually inspected after recovery.

All **40 searches** (twenty after refresh, twenty after offline recovery) match the unchanged
fixture oracle. Manifest hashes/counts, original Unsent hash and edited-work fingerprint are
verified independently. Recovery retains the new whole active generation, with airplane mode
on, Wi-Fi disabled and the fixture reverse route removed. Reopening leaves the saved refresh
complete; the offline-open report has its own ID and `offline-open-complete` status. The four
pending-operation process-stop cases are portable tests; this native force-close was after
refresh completion.

Largest sampled owner-store file allocation: **2,001.09 MiB** (2,098,293,382 bytes), not a continuous
peak. The retained end-of-refresh `Process.WorkingSet64` sample is **279.23 MiB**; it is identical
in saved/exported reports and is not peak or whole-app memory. The offline-open report's memory
sample remains unavailable, rather than borrowing the later export's working set.

| Evidence | Identifier |
|---|---|
| Refresh/current report | `tablet-spike-a8f2add1-4b35-4fbc-a313-9c9f2e1cfbf8.json` |
| Persisted refresh | `report-a8f2add1-4b35-4fbc-a313-9c9f2e1cfbf8.json` |
| Offline recovery report | `tablet-spike-87901e47-7ff1-4f45-af52-3cb2de6782af.json` |
| Active generation before | `21aba1a9c9d34ca3bcb5dda345e30b6a` |
| Active generation after/recovered | `b9b4616e15d14459aedb68a19331517c` |
| Edited work fingerprint | `193a2c1f273b464aecff293c18ca9e2b813936b20a86a5ab2d6b2637e3767075` |

Raw files, controller outcome, installed-APK proof, screenshots/XML, launch/memory/app logs and
the independently verified summary are under `.artifacts/wi032/phase-diagnostics/`. Analysis
also checks physical sample sums, separate phase/enclosing clocks and saved/exported memory
scope. The old favorable and incomplete reports remain unchanged and are not pooled with these.

## Cleanup and next boundary

After evidence retrieval, the identity-checked owned host was stopped; guest sync and verified
headless-engine exit stopped only the task AVD. Airplane mode was restored to zero and the task
reverse route was absent. The bounded wake helper exited after its stop marker. No shared adb
server, unrelated emulator, IDE, container, workload or user file was stopped/removed. Cleanup
records remain under `emulator/cleanup-result.json`. This increment did not perform an additional
whole-AVD restart after the new generation; its recovery evidence is the package-only cold open.

Stop for human review before another matrix or storage change, as required by the approved
increment and the delivery skill's “one increment at a time, stopping after each.” The new
phase evidence makes generation deletion a concrete review topic; no further optimization is
approved here. Physical Samsung/iPad measurements and the 1C runtime/storage decision remain
pending. WI-032 stays active and MI-02 stays open.
