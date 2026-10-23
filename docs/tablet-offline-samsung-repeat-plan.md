# WI-032 — repeated physical Samsung measurements

## Selection and approval boundary

Resume **WI-032 — Sign in on the tablet and download the day's data offline**, T-4.1.1,
REP-US-001 / IAM-US-003, Iteration 1 / phase-1. Sole active work item; Human Tight-Loop
implementation, Human-Led tests, Complex, High risk, Low confidence. Dependencies WI-002,
WI-021 and WI-005 are done; no card gate is newly opened or bypassed. The established console
adaptation reads `workItemStatus` from `plan_docs/field-sales-delivery/plan-data.js` instead
of an embedded HTML status block. Keep WI-032 active; do not edit the console or its cards.

The developer's **proceed** after pilot checkpoint `c508719` continues preparation of
**1B-B2**, the repeated Samsung matrix described in the parent device plan. This document
makes its run order, checks and capacity stop concrete. It awaits approval before new device
measurements. This is one increment, followed by a review stop; recovery/cancellation/owner
switching, iPad and adoption remain later work.

## Starting evidence and readiness

The [physical pilot](tablet-offline-samsung-baseline-pilot-results.md) verified three original
reports and sixty searches; baseline fresh/replacement refreshes 9.660/9.770 s, offline
local open 698.538 ms, worst observed-visible search 246.700 ms. Human scrolling/taps were
smooth. That pilot remains separate from this repeated set and is not counted as a repeat.

Selected physical USB device: **R52T30AZZXZ**, Samsung Tab S6 Lite **SM-P610**, Android
**13/API 33**, **Arm64**, active WebView **155.0.8059.43**. Reuse the installed Release
schema-v4 APK from source `4998512`, **45,026,457 bytes**, SHA-256
`8b51486e7130e41e7ba9809e3b2f5b898a2b3d95c4e29b69616fa4d794964ec5`.
No rebuild, reinstall, fixture regeneration, package/native update or source-suite repeat.
Existing verification remains **2,358 .NET / 34 JavaScript passes** for unchanged source.

Retained baseline generation `ee25e3ce860d4f4fae65d191ff1252a5`; edited Order note
**Samsung saved note WI-032**; work fingerprint
`89e54f6dc96aa4bdef76ff5efc96146553352e8c2d513463ca85c236c4747af9`.
Original Unsent SHA-256
`aca81e6cd733dfb7b5151607efccf441e92074cff6245ce9614f3f0552aacbf3`.
Readiness must verify these retained identities; never seed/reset a replacement to get them.

Planning read, **22 October 2026 at 14:25 UTC**: the ADB server was no longer running; the
client started it normally and the Samsung now reports **unauthorized**. No deliberate
server restart, installation, app input or settings change ran. The developer has been asked
to unlock/accept this PC's USB-debugging prompt. Fresh capacity, installed hash and foreground
could not be read. Do not select another device or kill/restart shared ADB to bypass this.
Evidence: `.artifacts/wi032/samsung-repeat-planning.json`.

## Capacity policy proposed for this increment

The last physical /data free point is **2,437,312 KiB (about 2.32 GiB)** from the completed
pilot, not a fresh guarantee. The older schema-v2 emulator stress set sampled up to
**2,098,892,670 bytes (about 1.95 GiB)** of app storage. Its other profile peaks also carried
large retained allocation, so they are not fresh baseline/target requirements. These are
different-device/APK point samples, not a physical upper bound or proof the Samsung will fit.

After USB authorization, record fresh capacity without clearing any data. Proposed guards:

| Check | Required free /data space | Action when below the guard |
|---|---:|---|
| Before entering baseline or target batch | 1 GiB | Do not start that batch; preserve prior results and stop for review. |
| Before entering stress batch | 3 GiB | Do not start stress; report the capacity stop with original baseline/target results. |
| Before every further Download & apply | 512 MiB | Do not start the next attempt; preserve the current generation/work. |

These are **proposed operational headroom guards (inferred)**, not product storage budgets
or measured minimums. The stress entry guard rounds up the older sampled maximum plus 25%
allowance and a 512 MiB reserve to 3 GiB, conservatively treating it as additional headroom.
The 1 GiB smaller-profile guard leaves room beyond their 16/64 MB JSON payloads and the
pilot's 68 MB sampled allocation. Neither guard guarantees capacity during an import.
Record free-space points between attempts and any native storage failure; no continuous-peak
claim. Crossing a guard is a valid stop, not a failed/fast stress measurement.

At the last known capacity the stress entry guard would **not pass**. Approval can therefore
yield twenty baseline/target operations and a documented capacity stop rather than thirty
executed operations. No data removal, cache clear, vacuum, retention change, smaller fixture
or reduced reserve is authorized to make stress fit. The human may arrange extra capacity;
resuming after a capacity stop needs review of the retained sequence and new conditions.

## One increment after approval

1. **Read-only preflight.** Human authorizes/unlocks this Samsung and makes it available for
   the run. Agent binds every command to its explicit serial, verifies physical model/OS/ABI/
   WebView and installed/archive APK hash, unchanged source, fixture/host hashes, saved note/
   fingerprint and active generation. Record initial actual network/wake state, reverse routes,
   free storage, memory, battery and thermal point samples. Existing spike may be brought
   forward once for readiness if necessary; record it as setup, outside measured cold opens.
   No hidden warm-up, new download or app reset. Further inputs require spike foreground;
   native popup exception requires both resumed activity and popup-node package identity.
2. **Owned USB fixture host.** Reuse the verified existing Debug host/runtime artifacts on
   `127.0.0.1:7206`, recording their hashes/configuration separately from the Release tablet.
   Refuse occupied port/reverse route; do not replace someone else's service. Start only an
   identified hidden host and bounded wake helper. Apply temporary tablet wake support only
   after recording its real initial state. No firewall or LAN exposure.
3. **Execute the matrix below, in order.** Check each profile's capacity guard before entering
   it. Keep the existing cache/retained generations between profiles. First target refresh
   replaces baseline, first stress refresh replaces target; label these transitions. Within
   each profile perform five consecutive replacements, then five consecutive offline process-
   cold opens. A guard/correctness/interruption stop prevents further cases; no skipped case
   is represented as executed. Case starts are persisted before action and cannot be replaced.
4. **Every replacement.** Verify profile/owner, server `http://127.0.0.1:7206/`, **USB adb
   reverse**, **Simulate 10 Mbit/s + initial 100 ms**, pause **None**. Enable only the owned
   route, then Download & apply exactly once. Run the twenty fixed terms once after completion
   and export the unique original report. Validate manifest/counts/hashes, SQLite 3.53.3/WAL/
   FULL, successful outcome, new generation, original fixture and unchanged edited work before
   the next attempt. Keep first-transition cost in statistics; do not invent fresh-store runs.
5. **Each profile's offline batch.** Remove only the owned route; temporarily enable airplane
   mode, disable Wi-Fi and confirm no active default network. Record fixture request count.
   Before each of five original opens, force-stop only the spike package and launch once with
   `am start -W`; persist launch/ready observations and export the new original report. Check
   generation equals the last successful replacement and edited work survives. Exported offline
   reports must have zero searches: this matrix's twenty-term batches belong to refreshes.
   Confirm no new host fixture requests across the batch. Do not launch a replacement case.
6. **Analysis and human review evidence.** Independently validate every available original
   report before advancing. Report per-profile replacement/local-open median and worst,
   all attempts/outcomes, search median/worst and target misses, stage/worker/continuation
   clocks, sampled disk and available end memory, generation transitions and actual conditions.
   Do not pool pilot/emulator/different-schema measurements. Ask the human about observed
   scrolling/touch/search responsiveness at the completed checkpoint; do not turn that into
   an extra hidden benchmark batch.
7. **Scoped cleanup and checkpoint.** Restore the recorded actual network/wake state, remove
   only the created serial-specific route, stop only identified host/wake helpers and preserve
   the app/cache/work on the Samsung. Copy/hash-verify temporary UI evidence before removing
   it; leave other apps/devices/shared services alone. Verify final installed hash and retained
   identities. Save results/index/plan checkpoint/notes, commit/push the same feature branch,
   then **STOP for review**, including partial outcomes when a guard or interruption stopped
   the matrix. WI-032 active; no PR, merge, main update or next card.

| Profile order | Products | Locations | History lines | Gzip bytes | Replacement attempts | Offline cold opens | Searches |
|---|---:|---:|---:|---:|---:|---:|---:|
| baseline | 5,000 | 500 | 60,000 | 1,570,674 | 5 | 5 | 100 |
| target | 25,000 | 500 | 120,000 | 6,294,888 | 5 | 5 | 100 |
| stress, capacity permitting | 100,000 | 2,000 | 1,440,000 | 33,229,222 | 5 | 5 | 100 |

Planned maximum: **thirty original reports, fifteen replacement attempts, fifteen offline
cold opens and 300 searches**, with all conditions/interruptions retained. Baseline/target
only, if stress is stopped before entry: **twenty reports / 200 searches**, explicitly partial.
Owner/fixture dates/seeds and twenty-term order remain unchanged. Re-entering online mode
after an offline batch requires re-verifying transfer selectors; they reset on process start.

Keep the pilot's bounds: 900 seconds per refresh, 45 seconds first observed offline readiness,
90 seconds per twenty-term batch, 600 seconds export retrieval. Persist original failures,
pending operations and unavailable timings. A correctness failure, controller cutoff, unexpected
foreground, changed artifact/device identity or USB interruption stops new actions. Recover
the same original report read-only when possible; do not repeat Download/search/launch or
force-close an in-flight refresh to get another result. Slow but correct completed results
remain observations and do not disappear from the matrix for missing a performance proxy.

## Human-Led scenario intents for approval

The parent storage scenarios remain the source; these specialize the repeated physical run
and add the explicit capacity stop. No new application or automated-test source is proposed.

| Scenario | Required evidence |
|---|---|
| Should_KeepTheSelectedDeviceAndArtifacts_When_RepeatingMeasurements | Same explicit physical serial/model, installed/source/fixture/host hashes and recorded runtime conditions; no rebuild/reinstall/reset/alternate device. |
| Should_StopBeforeAProfile_When_HeadroomIsInsufficient | Fresh free-space reading passes the specified entry guard; otherwise that profile remains unstarted and prior original outcomes are retained, without deleting data or shrinking fixtures. |
| Should_KeepEveryOriginalAttempt_When_ARefreshCompletesOrFails | Five original starts when a batch completes; fewer after an explicit stop, with unstarted cases listed. Every initiated attempt has its own outcome/evidence, verified new generation on success, manifest/hash/count checks and retained failures. No success-only replacement. |
| Should_KeepEditedWork_When_RefreshingAcrossProfilesOrReopening | The canonical four-item fingerprint/note/original Unsent identity persist across all completed replacements and opens, including profile transitions; active generation matches the latest completed replacement. |
| Should_ReturnExpectedMatches_When_ExecutingTheFixedTerms | Exactly twenty ordered terms per completed refresh; independently computed counts at 500/2,000 Locations; query and observed-visible clocks remain separate. |
| Should_ReopenTheRecordedDay_When_OfflineAndProcessCold | Five original COLD launches per executed profile, no owned route/default network/fixture request, same latest generation and saved work; raw seven local-open points and existing clock relationships retained. |
| Should_RecordConditionsAndUnknowns_When_SummarizingTheMatrix | Per-profile original-attempt counts, separate clocks, all misses/failures/cutoffs, numeric-only timing aggregates with denominators, point-sampled storage/end-memory availability and human responsiveness observation. No first-paint/physical-link/controlled-improvement claim. |
| Should_RestoreOnlyOwnedResources_When_TheIncrementEnds | Initial actual settings restored; own route/host/wake helper ended by identity; report hashes, final installed APK and saved work preserved; other devices/apps/services untouched. |

Review targets: the source's **500-Location observed-visible search under one second**;
candidate **60-second evaluation refresh / three-second local-open** proxies. Stress is a
capacity/timing diagnostic, not a promise of the one-second 500-Location target at 2,000.
Application simulation over USB does not certify 10 Mbit/s throughput or 100 ms physical RTT.
Observed-ready/activity/export clocks are separate from native operation/renderer clocks.
Offline JSON's reset Network selector default is retained as such; actual offline conditions
come from device/route/host evidence. Logical scans and Perfetto are not run. Native end
working-set values are samples, unavailable values stay null, and no continuous peak is claimed.

## Files, risks, exclusions and missing information

Plan and handoff: this file / `plan_docs/.agent-notes/WI-032.md`. Execution adds
`docs/tablet-offline-samsung-repeat-results.md`, an index entry in device results, checkpoint
here and notes. New controllers, case manifests, original reports, validation/oracle output,
before/after conditions, host logs and cleanup proof go under a new ignored
`.artifacts/wi032/samsung-repeat-session/`; prior pilot/emulator evidence is immutable.
No application/test/package/console change; reuse existing source-suite baseline. A necessary
source/runtime/storage-policy change stops for a focused amendment and appropriate tests.

Assumptions **(inferred)**: older sampled emulator allocation is useful only as a conservative
planning hint; selected headroom guards reduce risk but cannot prove physical capacity or
continuous free space. Thermal/background load/cache conditions can vary during the longer
matrix; record them rather than normalizing away slow outcomes. Guarded refusal to enter
stress is a capacity limitation, not product acceptance or an app failure.

Missing information: **USB reauthorization and fresh device readiness/capacity remain pending**.
They are explicit execution prerequisites and stop conditions, not deferred design choices.
No further manual model/OS/ABI input is needed. Approval accepts the stated capacity policy
and partial-matrix outcome if stress cannot start; it does not authorize reducing the guards.

Out of scope: freeing personal/device data, OS/WebView/package upgrade, storage tuning,
retention/cache reset, capture of native/managed stacks, stress retries after a stop, degraded
2 Mbit/s or actual LAN/link-shaping matrix, recovery/cancel/owner-isolation scenarios, iPad,
1C/runtime adoption, production sign-in/24-hour rule/Sync/MI-62, WI-033/034, PR/main integration.
The parent plan still owns those later physical/recovery/network checks; none is marked done.

The [console-delivery-next-item skill](C:/Users/colmk/.codex/skills/console-delivery-next-item/SKILL.md)
requires Human-Led scenarios to be agreed **before implementation begins** and tight-loop
execution **one increment at a time, stopping after each**. The pilot approval ended at
1B-B1. This concrete 1B-B2 scope, eight scenario intents and capacity policy await **Approved**.

## Execution checkpoint — 23 October 2026

The developer replied **Approved** to this plan and authorized USB debugging. The developer then
chose to use the fixture host rebuilt at `5a05963` from unchanged spike source, because the
pilot's host binaries had been overwritten and could not be reproduced. A first runner attempt
stopped before any case when the unpowered tablet's screen timed out. The screen timeout was
raised for the run under the wake-support allowance and restored at cleanup. Baseline and target
then completed all ten cases each. Stress stopped at its entry guard (2,077,944 KiB < 3 GiB) with
all ten stress cases unstarted. Results: [tablet-offline-samsung-repeat-results.md](tablet-offline-samsung-repeat-results.md).
Stop for review.
