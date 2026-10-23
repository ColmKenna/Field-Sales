# WI-032 — repeated physical Samsung measurement results

## Outcome — 23 October 2026

Approved increment **1B-B2 ran to its planned end with a capacity stop before stress**.
Baseline and target each completed **five replacements and five offline process-cold opens**:
**20 original reports, 100 replacement-search observations per profile (200 in all)**,
every one independently verified. No case was replaced, retried or skipped. **Stress was not
started:** /data had **2,077,944 KiB** free at its entry check, below the approved
**3,145,728 KiB (3 GiB)** guard. This is the plan's explicit partial outcome:
**20 of 30 reports and 200 of 300 searches**. The developer reports scrolling, taps and search
as **“Smooth”** on the target data after the run.

| Profile | Refresh · offline cases | Refresh s median / worst | Refresh &lt; 60 s | Offline local open ms median / worst | Local open &lt; 3 s | Android activity ms median / worst | Searches &lt; 1 s | Observed-visible ms median / worst | Query ms median / worst | Peak sampled storage bytes |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| baseline | 5/5 · 5/5 | 8.831 / 10.582 | 5/5 | 497.881 / 751.074 | 5/5 | 1,367 / 2,133 | 100/100 | 102.5 / 308.6 | 5.066 / 11.043 | 99,314,442 |
| target | 5/5 · 5/5 | 56.554 / 63.647 | 4/5 | 508.107 / 822.577 | 5/5 | 1,383 / 2,064 | 100/100 | 110.4 / 291.7 | 4.946 / 21.537 | 413,320,936 |

All 200 observed-visible searches are below the source's **one-second** target at 500
Locations. Every offline local open is below the candidate **three-second** proxy. All baseline
refreshes and **four of five target refreshes** are below the candidate **60-second** proxy:
`target-r5` took **63.647 s**. Medians and worsts use n=5 per profile
(n=100 for searches); no pilot, emulator or other-schema measurement is pooled. **WI-032 stays
active; stress capacity, recovery cases, the iPad check, MI-02 and adoption 1C remain open.**

## Per-case results

Refresh `totalRefresh` ends before report publication; `refreshAndLocalReopenAwait` adds
publication, UI continuation and the local reopen. Phase subsets overlap and are not summed.

| Case | Transition | Total refresh s | HTTP + hash s | Decompress/parse/insert/index s | Old generation deletion ms | Refresh + reopen s | Reopen ms | Worst query ms | Worst observed-visible ms | Peak sampled storage bytes |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| baseline-r1 | first baseline refresh replaces the retained pilot baseline | 10.582 | 1.492 | 7.636 | 695.1 | 10.687 | 19.5 | 10.867 | 277.1 | 98,783,082 |
| baseline-r2 | same-profile replacement | 8.898 | 1.390 | 6.449 | 748.9 | 8.923 | 15.5 | 8.977 | 308.6 | 99,240,402 |
| baseline-r3 | same-profile replacement | 8.349 | 1.399 | 6.221 | 321.6 | 8.374 | 14.3 | 9.244 | 153.9 | 99,314,442 |
| baseline-r4 | same-profile replacement | 8.831 | 1.416 | 6.524 | 601.2 | 8.868 | 25.7 | 8.235 | 135.9 | 99,207,130 |
| baseline-r5 | same-profile replacement | 8.669 | 1.405 | 6.425 | 471.0 | 8.714 | 30.9 | 11.043 | 140.0 | 98,935,210 |
| target-r1 | first target refresh replaces baseline | 47.386 | 5.270 | 38.145 | 3,104.9 | 47.514 | 44.9 | 8.136 | 291.7 | 203,283,464 |
| target-r2 | same-profile replacement | 57.234 | 5.176 | 50.929 | 834.8 | 57.267 | 22.5 | 9.619 | 187.9 | 303,207,760 |
| target-r3 | same-profile replacement | 55.803 | 5.187 | 46.721 | 3,582.8 | 55.836 | 23.0 | 21.537 | 132.9 | 410,660,160 |
| target-r4 | same-profile replacement | 56.554 | 5.198 | 48.296 | 2,640.2 | 56.607 | 37.8 | 8.690 | 140.4 | 413,320,936 |
| target-r5 | same-profile replacement | 63.647 | 5.188 | 49.687 | 8,134.5 | 63.699 | 36.3 | 8.837 | 282.6 | 413,246,776 |

Offline local-open clocks come from each report; the Android activity clock is `am start -W`
TotalTime; the readiness bound runs from before `am start` until UI inspection succeeds and
includes the host's uiautomator polling. None is first paint, and they are not added together.

| Case | Launch | Local open ms | Worker ms | Continuation ms | Dispatch ms | Android activity ms | Host readiness bound ms |
|---|---|---:|---:|---:|---:|---:|---:|
| baseline-o1 | COLD | 751.074 | 690.172 | 8.630 | 49.634 | 2,133 | 8,892.043 |
| baseline-o2 | COLD | 537.971 | 467.823 | 10.092 | 54.962 | 1,362 | 7,664.105 |
| baseline-o3 | COLD | 492.102 | 436.832 | 13.886 | 36.529 | 1,367 | 7,597.220 |
| baseline-o4 | COLD | 497.881 | 444.136 | 13.462 | 35.110 | 1,349 | 7,580.830 |
| baseline-o5 | COLD | 495.293 | 434.840 | 17.343 | 36.616 | 1,408 | 7,525.787 |
| target-o1 | COLD | 822.577 | 760.206 | 9.361 | 50.057 | 2,064 | 8,770.650 |
| target-o2 | COLD | 505.186 | 451.987 | 13.286 | 34.713 | 1,446 | 7,672.399 |
| target-o3 | COLD | 503.682 | 445.100 | 17.188 | 34.483 | 1,326 | 7,585.262 |
| target-o4 | COLD | 542.794 | 494.666 | 9.720 | 34.414 | 1,383 | 7,756.050 |
| target-o5 | COLD | 508.107 | 448.712 | 17.358 | 35.120 | 1,288 | 7,489.604 |

### Observations, not established causes

- Target refresh time is dominated by import: entity insert/projection/commit took
  **30.4–42.6 s** of each target refresh. HTTP plus hash (about 5.2 s for
  6,294,888 gzip bytes) is consistent with the application's 10 Mbit/s simulation.
- The first target refresh, replacing baseline, was the fastest (47.386 s).
  Same-profile target replacements took 55.803–63.647 s. Completed
  generation deletion ranged **0.835–8.135 s**. `target-r5`'s
  8.135 s deletion was its largest difference from the other replacements.
  With n=5 and uncontrolled storage/background conditions, the cause is not established.
- The first offline open of each batch (751.1, 822.6 ms) was slower than the
  following eight (492.1–542.8 ms). OS caching is uncontrolled.
  This run does not establish a cause.
- These physical runs did not reproduce the stress-scale emulator's multi-second local-open
  continuation; worker-to-continuation stayed at 8.630–17.358 ms.
  Stress was not run, so the scale at which that emulator gap appeared remains untested here.

## Device, artifacts and conditions

Samsung **Tab S6 Lite SM-P610**, explicit USB serial **R52T30AZZXZ**, physical device
(`ro.kernel.qemu=0`), Android **13/API 33**, **Arm64**, active WebView
**155.0.8059.43**, all matching every report. The installed Release schema-v4 APK is unchanged:
**45,026,457 bytes**, SHA-256 `8b51486e7130e41e7ba9809e3b2f5b898a2b3d95c4e29b69616fa4d794964ec5`, verified at preflight
and cleanup and matching the archive. Native SQLite **3.53.3 / WAL / FULL**. No app, test, package
or spike-source change (spike paths unchanged since `4998512`), no reinstall, reset, cache clear
or data removal, and no Perfetto capture or logical scan.

**Fixture host deviation (developer-approved).** The pilot's Debug host binaries were
overwritten by the 23 October merge-verification build. Rebuilding with five candidate source
revisions did not reproduce their hashes. The developer chose the host rebuilt at `5a05963`
from unchanged spike source (`1.0.0+5a05963b2355d9d692d5c8a4f12476d08c645c7e`); its hashes are recorded
separately in `host-artifact-hashes.json` and `host-provenance.json`. The host's job, serving
fixtures with the application-level simulation, uses unchanged source; the tablet build is
unaffected. Host and tablet results should still be read as a separate run from the pilot.

Fixtures (seed 42, as of 2026-10-20) were verified against their manifests: baseline
5,000 products / 500 Locations / 60,000 history lines (1,570,674 gzip bytes); target
25,000 / 500 / 120,000 (6,294,888 gzip bytes); stress 100,000 / 2,000 / 1,440,000
(33,229,222 gzip bytes; not downloaded). Every replacement verified profile, owner
`spike-rep-42`, server `http://127.0.0.1:7206/`, **USB adb reverse**, **Simulate 10 Mbit/s +
initial 100 ms** and pause **None** before Download & apply. The simulation runs over USB; it
does not certify physical throughput or RTT.

For each offline batch only the owned route was removed, airplane mode enabled, Wi-Fi disabled
and `Active default network: none` confirmed. Fixture requests stayed at
**10** across baseline's batch and **20** across target's
(two per refresh): **zero new requests** during any offline open. Offline reports keep the
app's reset Network selector default; actual offline conditions come from these device and
host records.

Free /data points: preflight **2,373,076 KiB**, baseline entry **2,372,004**,
target entry **2,367,020**, stress entry **2,077,944 KiB**. Device memory
at preflight: 3,797,324 KiB total / 1,206,476 KiB available. Battery 91% → 85%, temperature
17.9 °C → 21.7 °C. The tablet was discharging throughout: this USB connection does not
power it. End-of-refresh `Process.WorkingSet64` samples were 244,023,296–332,685,312 bytes.
Offline working set is unavailable. Storage values are point samples, not continuous peaks.

**First attempt aborted before any case.** Because the tablet is not powered over USB,
`stay_on_while_plugged_in` had no effect, and the screen timed out between readiness and the
matrix start. The runner's foreground guard refused all input. With the plan's temporary
wake-support allowance, the screen timeout was raised from 120,000 ms to 1,800,000 ms for the
run (restored at cleanup). A wake key returned the screen before the 10-minute lock delay, and the
spike was brought forward again (HOT, same process) as setup. Readiness and re-focus were not
measured. Records are in `attempt-1-aborted/`, `screen-timeout-before.json` and `wake-support.json`.

## Saved work, generations and searches

The retained pilot generation `ee25e3ce860d4f4fae65d191ff1252a5`, note **Samsung saved note
WI-032** and fingerprint `89e54f6dc96aa4bdef76ff5efc96146553352e8c2d513463ca85c236c4747af9` were verified before the first case.
Every report's before/after fingerprint matches the independently reconstructed canonical
work object. Each refresh produced a new generation from its predecessor; each offline open
shows the latest successful refresh's generation. The target data and the edited work remain on the tablet.

| Case | RunId | Generation | Original report SHA-256 |
|---|---|---|---|
| baseline-r1 | `866b38b3-3fc1-4e25-8ff7-85de7fd03899` | `8bd0b97b1c304d31b38a2c65f0281038` | `f00d14c8fe0a57bf18f7312482ae03dd1d2a49be24508efec0e4b3d43aebfb35` |
| baseline-r2 | `f57d9bdd-2112-4e49-a61d-bf0b1e0a6c8e` | `d8a28a98890e487ca2cf68bfb302d65b` | `34dff0a2d0b92085836347e8e5a3e74c42caa819fdc30534aa8a62e985cbdfbe` |
| baseline-r3 | `cdd537a1-1bdc-4713-9339-8b0631691d1a` | `3d159b74eded4004b6982c066b995774` | `da5219da539b393edecce5f1ec30b8ef16c5f71a6234484b77d4993ed8388194` |
| baseline-r4 | `9f954434-6cf0-4e31-b3c1-aecc3b63966e` | `196f839a63aa41f6a379c646ed8d5091` | `138d54d0ab84fa7907fb9fcf0c6a44bfe14a0281de9f97c1fdcda3176153d27b` |
| baseline-r5 | `6a723bca-aa81-4e5d-973c-779d3aee1512` | `b5d72517ca8148b9bbb85839c695a122` | `15d154a9fc9b8cab19d8bc2f16b671246282baac8d77f5f853763a089cf0129e` |
| baseline-o1 | `45a85afa-b124-4c38-a892-4b4bf5b4af15` | `b5d72517ca8148b9bbb85839c695a122` | `074c25ca70bcc1ec95906de5f5dd714e9e793ba47ee4b90e021b4127e8e3351a` |
| baseline-o2 | `f470b128-d623-441a-a60e-4db4e649187c` | `b5d72517ca8148b9bbb85839c695a122` | `8578cf2193e3d683c1d65311f80b366054bb739f8652aefed25836545096a2f0` |
| baseline-o3 | `99b4cad4-673d-42e5-b8d1-511bd366f5c8` | `b5d72517ca8148b9bbb85839c695a122` | `ca0b13f87a7410d7d1ab8cfce520a2d2b5bba706b67e956b1e99b862f59e8027` |
| baseline-o4 | `d9aa440b-1a8f-44fd-8160-4526e7925bb5` | `b5d72517ca8148b9bbb85839c695a122` | `8af204688951121bbd805bb4f3fc919bfad02a34e0ad44742e1d465aeb5b4aa1` |
| baseline-o5 | `f612103e-38a5-4f7f-88a4-3b99f8861871` | `b5d72517ca8148b9bbb85839c695a122` | `5d45a226540be92e3d4fd6d01d303470abcf94475bc1856383091bafe0b19a17` |
| target-r1 | `e5eecd80-b157-432c-901a-2d1f17cb1f5e` | `e5f6505b6c5e47498bf47bcf87be58e4` | `d2dbb29dcbe21103b5e583ab7970f2d51e2115fec48c27fbd46fe714c5d2649a` |
| target-r2 | `998d27af-3106-4c05-b7b3-67505b84fd47` | `7731e5a8237f4201a1150c488fa4a255` | `150d4718e3720f66ce6ebccbb0c3bc5df903934fa93f730309a4911a8e81c249` |
| target-r3 | `f708fa8b-c69f-4b9c-8569-e9c04a34e8eb` | `ebeb78cf273f4815bcba80b77b222c7d` | `1febae722a82233f2faea1591c0fca9944a252d7fbbc820fb9e0c191af96d1f0` |
| target-r4 | `26174169-b970-40f5-8bc9-1656736f0b86` | `4cc9dcf406de4ce0a731c5cd02444d28` | `8fd3079064d7c3620f11030ed45a34e7a911916de22137ab4f8c05c7cffbf572` |
| target-r5 | `62213997-3a77-4509-8cb8-1e55fb07207f` | `8f4acbfc67bf4facaae3053653b55fdd` | `d0f608a4f529da0d23570864607ff9ac0dca1a9dcf12886e3f0ef332f9e953cc` |
| target-o1 | `74edb8ba-c13c-4c2b-95f1-ea76b367cefb` | `8f4acbfc67bf4facaae3053653b55fdd` | `cba5f8ee262aa7cd7dd40020a77807b19fb998e63aed97c9f121d159feec4b95` |
| target-o2 | `413303b6-dad7-469a-b618-1c05babd952c` | `8f4acbfc67bf4facaae3053653b55fdd` | `8c115ae42b118ae059e49449d4835976eaa2d1e938d0f1176f17e569b0dab761` |
| target-o3 | `da590065-6698-4d09-8cda-abd66f1b7674` | `8f4acbfc67bf4facaae3053653b55fdd` | `1d849dd8e64f34cea20d6122dff88ef5468e94e7eea058763eb712e0ed1b941a` |
| target-o4 | `dca682c4-5396-494e-829c-e9e9c31d02f8` | `8f4acbfc67bf4facaae3053653b55fdd` | `e530cb0f1cb642ce9187bab5969e03e700f486c7d4bf9d441dbeae21241808e7` |
| target-o5 | `35eab50a-33d3-4b88-a442-abaf2fd144e3` | `8f4acbfc67bf4facaae3053653b55fdd` | `94ef9a65b9ab2a333af809a9a91b094b2332a364289ed58edd6172327207f34c` |

Each refresh ran the twenty fixed terms once, in order. Expected counts are computed
independently from the generator's six-name/six-town cycle at 500 Locations, normalizing accents,
case and apostrophes. All 200 counts agree. Query and observed-visible clocks remain separate.
Observed-visible timing runs from the JS call to after render; it is not keystroke-to-photon.

## Scenario verification

| Agreed intent | Result / evidence |
|---|---|
| Keep the selected device and artifacts | Pass with the approved host deviation: same serial/model/OS/ABI/WebView, installed and archived APK, fixture and Unsent hashes; no rebuild of the app, reinstall, reset or other device. |
| Stop before a profile when headroom is insufficient | Pass: baseline and target entry passed the 1 GiB guard. Stress stayed unstarted at 2,077,944 KiB &lt; 3 GiB, and all ten stress cases are listed as unstarted. Nothing was deleted and no fixture was shrunk. |
| Keep every original attempt | Pass: 10 refreshes started and 10 completed. Each has its own persisted start, outcome, report and verified new generation, plus manifest/hash/count checks. No failures occurred and none was replaced. |
| Keep edited work across profiles and reopens | Pass: the canonical fingerprint/note and original Unsent identity persist through all 20 cases, including the baseline-to-target transition. |
| Return expected matches | Pass: 20 ordered terms per refresh, 200 independent count checks at 500 Locations, separate query and visible clocks. |
| Reopen the recorded day offline and process-cold | Pass: 10 COLD launches, no route or default network, no fixture requests, latest generation and saved work every time, seven raw local-open points each. |
| Record conditions and unknowns | Pass: per-profile counts, separate clocks, the one 60-second proxy miss, numeric aggregates with denominators, point-sampled storage, unavailable offline memory, the aborted first attempt and the developer's “Smooth”. |
| Restore only owned resources | Pass: airplane 0 / Wi-Fi 1 / stay-on 0 / screen timeout 120,000 ms restored, owned route removed, host and wake helper verified exited, temporary UI dump copied then removed, APK and saved work retained. |

## Lifecycle and evidence

Readiness brought the already-running spike forward (not a cold launch), started the owned
host on `127.0.0.1:7206`, created the serial's USB reverse route and a bounded PC wake helper.
Cleanup restored the recorded settings and stopped only those identified processes. Other apps,
devices and shared services were not operated.

Evidence: `.artifacts/wi032/samsung-repeat-session/`, including `summary.json`,
`validate-report.py`, the 20 original reports, per-case start/ready/observation/validation and
launch records, UI dumps, space points, host logs and hashes, device conditions, the human
observation and cleanup proof. Unchanged-source verification remains **2,358 .NET / 34
JavaScript passes at `4998512`**; this measurement increment did not rebuild the app or repeat
the source suites.

Stop for Human Tight-Loop review. Open items: stress needs about 1 GiB more free space, or a
decision on the guard. Recovery/cancellation/owner-isolation cases, the iPad mini/Mac check,
MI-02 and adoption 1C remain later work. No optimization, PR, merge or main integration follows.
