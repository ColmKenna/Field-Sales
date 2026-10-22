# WI-032 — physical Samsung baseline pilot results

## Outcome — 22 October 2026

Approved checkpoint **1B-B1 is complete**: one fresh baseline download, one edited-work
replacement and one original offline process-cold open. **Three original reports and all
sixty search results independently verify**, with no operation or batch replaced. The
developer confirms **“Yes—scrolling and taps respond smoothly”** in the post-pilot check.
All seven [approved pilot scenarios](tablet-offline-samsung-validation-plan.md) are checked.

The offline local-open operation took **698.538 ms**, including
**637.404 ms** worker body and **8.755 ms** worker-end-to-
continuation. Its Android activity clock is separately **2,097 ms**;
the host's first observed-ready polling bound is **8,705.791 ms**.
Neither is first paint or added to the local-open operation. The previous stress-scale
emulator draw delay is not established or ruled out by this single physical baseline open.

| Original operation | Total refresh s | Local reopen ms | Worker ms | Continuation ms | Worst query ms | Worst observed-visible ms |
|---|---:|---:|---:|---:|---:|---:|
| setup | 9.660 | 376.620 | 373.907 | 1.991 | 9.562 | 146.200 |
| replacement | 9.770 | 50.344 | 33.104 | 16.440 | 6.735 | 198.200 |
| offline | — | 698.538 | 637.404 | 8.755 | 20.317 | 246.700 |

All **60/60** observed-visible searches are below the source's one-second target at 500
Locations. Both refreshes are below the candidate 60-second proxy; the one offline local
open is below the candidate three-second proxy. These are individual pilot observations,
not repeated-distribution statistics or acceptance of the full Samsung/iPad storage spike.
**WI-032 stays active; MI-02 and adoption 1C remain open.**

## Device, artifacts and method

Samsung **Tab S6 Lite SM-P610**, explicit USB serial **R52T30AZZXZ**, physical device
(`ro.kernel.qemu=0`), Android **13/API 33**, runtime **Arm64**. Active WebView
`com.google.android.webview` **155.0.8059.43** matches each report's user agent. The installed
non-debuggable, profileable **Release schema-v4** APK is unchanged from source `4998512`:
**45,026,457 bytes**, SHA-256
`8b51486e7130e41e7ba9809e3b2f5b898a2b3d95c4e29b69616fa4d794964ec5`. Archived and installed hashes match before/after; prior signature/ABI/
native-alignment inspection is retained, and installation/loading succeeded on this device.
Native SQLite reports **3.53.3 / WAL / synchronous FULL**. No app/test/package/source change,
new build, cache/data clear, OS/WebView upgrade, emulator operation or Perfetto capture.
All seven raw local-open boundary records are present; native trace enabled flags are false.
No scheduler attribution is made from these physical reports.

The initial inventory was empty; the developer's search-again found the authorized USB
device. Execution then waited at the lock screen until the developer confirmed it was
unlocked. The first planned launch opened the empty spike for setup; it is retained as
setup, not an additional measured offline open or warm-up. The subsequent cold launch is
the one offline case. OS caches, background load and device conditions remain uncontrolled.
The active UI was landscape (2000×1200); physical panel dimensions report 1200×2000.

Reused synthetic seed 42/as-of 2026-10-20: **5,000 products, 500 Locations and 60,000 history
lines**, full future-payload baseline, **16,441,340 JSON / 1,570,674 gzip bytes**. Both reports
verify every manifest count, payload hashes, owner and original Unsent fixture identity.
The fixture host used the existing **Debug** host/runtime artifacts, separately recorded
from the tablet's **Release** app. Their hashes were archived; no rebuild occurred.

Only loopback port 7206 and this serial's USB reverse were used. Both downloads selected
**10 Mbit/s plus initial 100 ms application simulation over USB adb reverse**. This does not
measure physical throughput/RTT. For the offline case the route was removed, airplane mode
enabled, Wi-Fi disabled and `Active default network: none` verified. The host logged five
fixture requests before and five after the offline case: **zero new fixture requests**.
The offline report's Network field is the app's reset selector default (`LAN; physical
conditions unmeasured`); it is not actual connectivity evidence. Original JSON is retained
unchanged; actual offline conditions are established by the separate device/host records.

## Saved work and independent search checks

Setup original work fingerprint:
`a531a4aef78175a4e29fb4b52e073247ad73ee8489b956e2d8862d3f0bbff4c5`.
The synthetic Order note was changed to **Samsung saved note WI-032**. Its saved UI
checkpoint, replacement before/after and offline before/after all match:
`89e54f6dc96aa4bdef76ff5efc96146553352e8c2d513463ca85c236c4747af9`.
The oracle independently reconstructs the canonical work object from the original fixture,
sorted by item ID/properties, changing only the Order note. This verifies the two Calls,
Order, schedule change, payloads and captured-under references through the logical fingerprint;
SQLite file-byte identity is not used. The original fixture SHA-256 remains
`aca81e6cd733dfb7b5151607efccf441e92074cff6245ce9614f3f0552aacbf3`.
SaveNote does not update old report fields: setup was exported before the edit, and the
edited expectation was taken from the saved UI before starting replacement.

Each original batch has exactly the twenty specified terms in order. Expected match counts
are computed independently from the baseline's six-name/town cycle, normalizing accents,
case and straight/curly apostrophes. Every count agrees, including the no-match term.
Automated observed-visible timing starts at the JS invocation and ends after render/two
animation frames; it is not a physical keystroke-to-photon measurement. The developer's
post-pilot scrolling/tap check supplies separate human responsiveness evidence.

| Operation | RunId | Generation | Original report SHA-256 |
|---|---|---|---|
| setup | `31e7203d-e5b4-4223-a9c9-c0f6b28873e7` | `f661b2e35c0e401897555e4a090c6a18` | `86c0b918fe91373ab3efafa2762a6ec7b1255279d92ae772157f9e4a6f578161` |
| replacement | `b074e65e-3214-4a10-9a2e-172d83b5d109` | `ee25e3ce860d4f4fae65d191ff1252a5` | `e7388f3a1c6deb4c74af92206772b0278ad24aec5f03fa2b457b8d5b3c19ee4c` |
| offline | `d3d40c89-d867-4fa1-a4c8-c93df7a2a4d8` | `ee25e3ce860d4f4fae65d191ff1252a5` | `a420c3cae3f51181db373cdb779c37426ab98cf3de5f98499f175b9cb5168bc8` |

## Disk, memory and clock limits

| Operation | Peak sampled storage bytes | Post-input export polling bound ms |
|---|---:|---:|
| setup | 37,392,826 | 723.044 |
| replacement | 67,851,066 | 126.713 |
| offline | 60,813,312 | 724.144 |

Storage samples include snapshot DB/WAL/journal/SHM, transfer and Unsent files; retained
generations/free allocation are included, while APK/OS/reports/logs are excluded. These are
point-sampled maxima, not continuous physical peaks. Logical payload scans were not run;
their totals/clocks remain unavailable. End-of-refresh `Process.WorkingSet64` samples are
**361,316,352 / 355,909,632 bytes** for setup/replacement, not peak RSS. Offline memory is
unavailable. No missing metric is presented as zero.

Initial device memory point: **3,797,324 KiB total / 1,283,476 KiB available**; /data free
**2,566,688 KiB before / 2,437,312 KiB after**. Battery **91% before / 90% after**. Battery
temperature reports **17.0°C / 17.6°C**; thermal HAL records are retained. These points do not
describe all conditions during each attempt. The pilot does not establish stress capacity.
Refresh pipeline subsets overlap enclosing clocks and are not summed. `totalRefresh` ends
before final report publication; `refreshAwait` includes publication/UI continuation;
`refreshAndLocalReopenAwait` additionally includes local reopen. Raw phase scopes remain in
each report. Export polling bounds exclude the input command and are not native-write latency.

## Scenario verification and lifecycle

| Agreed intent | Result / evidence |
|---|---|
| Selected Samsung only | Pass: explicit USB serial/model/physical/OS/ABI/WebView and installed hash verified; no other device operation. |
| Whole baseline import | Pass: both original completed downloads match full manifest/hash/counts, owner and SQLite settings. |
| Edited work survives | Pass: independent canonical fingerprints, saved note, replacement preservation and unchanged offline generation. |
| Expected Locations | Pass: all sixty term/order/count checks; all observed-visible clocks under one second; human scrolling/taps smooth. |
| Offline cold open | Pass: one COLD launch, no route/default network, preserved generation/work, zero host requests. |
| Honest original reports | Pass: three unique run/attempt IDs, hash-verified pulls, raw statuses/clocks/availability retained; no replacement/retry. |
| Scoped cleanup | Pass: actual initial settings restored, owned host/wake helper verified exited, reverse route absent and installed APK/data retained. |

Two automation guards stopped input safely: the native selector's focus label was only
`PopupWindow`; continued selection required both the resumed spike activity and every popup
node belonging to the spike. Later, an immediate foreground check still saw the share chooser
after Back; the next read confirmed it had closed, without sending another Back. No Download,
search batch or launch was repeated to replace a measurement. There were no native operation
failures or observation-cutoff overruns. Selector/UI evidence and guard metadata are retained.

Each report was retrieved with matching device/local hash. Samsung's verified
`android/com.android.internal.app.ChooserActivity` was dismissed using Back only; no recipient
was selected. Cleanup copied/hash-verified then removed only the temporary UI dump. Actual
initial **airplane 0 / Wi-Fi 1 / stay-on 0** are restored. The screen was originally dozing
at preflight; the human unlocked it before agent mutations. That fresh awake state was
recorded and the screen left awake. Own host PID/start time/command line and cooperative wake
helper exit verify. Route is absent; installed APK, baseline generations and edited work
remain on the Samsung. Other apps/devices/shared services were not stopped or operated.

Evidence: `.artifacts/wi032/samsung-baseline-pilot/`, including `summary.json`, independent
`validate-report.py`, original three reports and observations, start/launch/UI records, note
checkpoint, human response, fixture/host hashes/logs, device conditions and cleanup proof.
Unchanged-source verification remains **2,358 .NET / 34 JavaScript passes at `4998512`**;
this measurement/docs increment did not rebuild or repeat source suites. `git diff --check`
and unchanged-source/artifact checks apply to this checkpoint.

Stop for Human Tight-Loop review. Proposed next increment: the existing five-refresh/five-
offline-open matrix per baseline/target/stress, with fresh device-capacity/condition checks.
Recovery cases, iPad mini/Mac validation, MI-02/1C adoption and production WI-032 work remain
pending. No optimization, full-matrix execution, WI-033, PR/merge or main integration follows
automatically from this pilot.
