# WI-032 — repeated physical Samsung measurement results

## Outcome — 23 October 2026

Approved increment **1B-B2 is complete for all three profiles**. Baseline, target and stress
each completed **five replacements and five offline process-cold opens**: **30 original reports
and 300 search observations**, every one independently verified. No case was replaced, retried
or skipped. Stress first **stopped at its entry guard** (2,077,944 KiB free, below
3,145,728 KiB). After the developer freed space, a stress-only continuation passed the same guard
at **12,482,340 KiB** and ran all ten stress cases. The developer reports
scrolling, taps and search as **“Smooth”** on both the target and the stress data.

| Profile | Locations | Refresh · offline cases | Refresh s median / worst | Refresh &lt; 60 s | Offline local open ms median / worst | Local open &lt; 3 s | Android activity ms median / worst | Searches &lt; 1 s | Observed-visible ms median / worst | Query ms median / worst | Peak sampled storage bytes |
|---|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| baseline | 500 | 5/5 · 5/5 | 8.831 / 10.582 | 5/5 | 497.881 / 751.074 | 5/5 | 1,367 / 2,133 | 100/100 | 102.5 / 308.6 | 5.066 / 11.043 | 99,314,442 |
| target | 500 | 5/5 · 5/5 | 56.554 / 63.647 | 4/5 | 508.107 / 822.577 | 5/5 | 1,383 / 2,064 | 100/100 | 110.4 / 291.7 | 4.946 / 21.537 | 413,320,936 |
| stress | 2,000 | 5/5 · 5/5 | 227.296 / 228.802 | 0/5 | 527.450 / 1,082.774 | 5/5 | 1,580 / 2,393 | 100/100 | 116.4 / 293.5 | 11.085 / 72.825 | 2,096,320,078 |

All **300** observed-visible searches are below one second, including the **100 at 2,000
Locations**; the source's one-second target applies to 500 Locations, and stress is a
diagnostic beyond it. All **15** offline local opens are below the candidate three-second proxy.
Against the candidate 60-second refresh proxy: baseline **5/5**, target **4/5** (`target-r5`
63.647 s), stress **0/5** (median 227.296 s). Medians and
worsts use n=5 per profile (n=100 for searches). No pilot, emulator or other-schema measurement
is pooled. **WI-032 stays active; recovery cases, the iPad check, MI-02 and adoption 1C remain open.**

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
| stress-r1 | first stress refresh replaces target | 215.538 | 26.834 | 169.332 | 17,773.6 | 215.670 | 37.4 | 48.568 | 293.5 | 976,456,822 |
| stress-r2 | same-profile replacement | 222.122 | 26.724 | 179.379 | 15,330.0 | 222.151 | 17.5 | 19.902 | 146.1 | 1,514,226,382 |
| stress-r3 | same-profile replacement | 228.802 | 26.710 | 173.081 | 28,078.2 | 228.844 | 27.6 | 72.825 | 165.2 | 2,096,320,078 |
| stress-r4 | same-profile replacement | 228.564 | 26.723 | 179.114 | 21,965.4 | 228.596 | 19.1 | 67.851 | 146.3 | 2,091,673,310 |
| stress-r5 | same-profile replacement | 227.296 | 26.731 | 175.362 | 24,504.8 | 227.401 | 90.0 | 53.004 | 144.7 | 2,096,135,270 |

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
| stress-o1 | COLD | 1,082.774 | 1,014.140 | 12.566 | 52.024 | 2,393 | 9,604.322 |
| stress-o2 | COLD | 540.115 | 490.175 | 8.681 | 38.834 | 1,824 | 8,114.765 |
| stress-o3 | COLD | 527.450 | 479.470 | 8.280 | 34.493 | 1,580 | 7,837.401 |
| stress-o4 | COLD | 485.698 | 431.403 | 10.126 | 40.537 | 1,509 | 7,715.889 |
| stress-o5 | COLD | 509.017 | 463.635 | 9.241 | 33.888 | 1,420 | 7,483.875 |

### Observations, not established causes

- **Import dominates refresh time.** Entity insert/projection/commit took
  30.4–42.6 s per target refresh and
  131.4–140.6 s per stress refresh. HTTP plus hash
  (5.2–5.3 s target, 26.7–26.8 s stress) matches the
  application's 10 Mbit/s simulation for 6,294,888 and 33,229,222 gzip bytes.
- **Old-generation deletion grows with scale and varies:** 0.835–8.135 s
  at target and 15.330–28.078 s at stress. `target-r5`'s
  8.135 s deletion was its largest difference from the other target replacements.
  The first target refresh, replacing the smaller baseline, was the fastest target case.
- **The first offline open of each batch was the slowest** (751.1, 822.6, 1,082.8 ms,
  baseline/target/stress); the other twelve took 485.7–542.8 ms. OS caching is uncontrolled.
- **No multi-second local-open continuation on this tablet.** Worker-to-continuation stayed at
  8.280–17.358 ms across all 15 opens, including 8.280–12.566 ms at stress. The
  stress-scale emulator traces showed about 2.5–2.7 s there. This is consistent with the earlier
  inference that the emulator gap is a software-graphics effect. It is n=5 on one device and is
  not a controlled comparison.
- **Stress storage:** peak sampled storage reached 2,096,320,078 bytes. Free /data fell from
  12,484,880 to 11,059,272 KiB across the five stress refreshes. These are
  point samples, not continuous peaks.

## Device, artifacts and conditions

Samsung **Tab S6 Lite SM-P610**, explicit USB serial **R52T30AZZXZ**, physical device
(`ro.kernel.qemu=0`), Android **13/API 33**, **Arm64**, active WebView
**155.0.8059.43**, all matching every report. The installed Release schema-v4 APK is unchanged:
**45,026,457 bytes**, SHA-256 `8b51486e7130e41e7ba9809e3b2f5b898a2b3d95c4e29b69616fa4d794964ec5`, verified at preflight,
both readiness steps and both cleanups, and matching the archive. Native SQLite **3.53.3 / WAL /
FULL**. No app, test, package or spike-source change (spike paths unchanged since `4998512`), no
reinstall, reset, cache clear or app-data removal, and no Perfetto capture or logical scan.

**Fixture host deviation (developer-approved).** The pilot's Debug host binaries were
overwritten by the 23 October merge-verification build. Rebuilding with five candidate source
revisions did not reproduce their hashes. The developer chose the host rebuilt at `5a05963`
from unchanged spike source (`1.0.0+5a05963b2355d9d692d5c8a4f12476d08c645c7e`). Its hashes are recorded separately in
`host-artifact-hashes.json` and `host-provenance.json`, and were re-verified before the stress
continuation. The tablet build is unaffected; read these results as a separate run from the pilot.

Fixtures (seed 42, as of 2026-10-20) were verified against their manifests: baseline
5,000 products / 500 Locations / 60,000 history lines (1,570,674 gzip bytes); target
25,000 / 500 / 120,000 (6,294,888); stress 100,000 / 2,000 / 1,440,000 (33,229,222). Every
replacement verified profile, owner `spike-rep-42`, server `http://127.0.0.1:7206/`,
**USB adb reverse**, **Simulate 10 Mbit/s + initial 100 ms** and pause **None** before
Download & apply. The simulation runs over USB; it does not certify physical throughput or RTT.

For each offline batch only the owned route was removed, airplane mode enabled, Wi-Fi disabled
and `Active default network: none` confirmed. Fixture requests were unchanged across every batch:
baseline 10, target 20 (one host log for both), stress
10 (its own host log), two per refresh. That is **zero new requests** during any offline open.

Free /data points: preflight **2,373,076 KiB**, baseline entry **2,372,004**,
target entry **2,367,020**, first stress entry **2,077,944**, stress continuation
entry **12,482,340**, after the last stress refresh **11,059,272 KiB**.
Device memory at preflight: 3,797,324 KiB total / 1,206,476 KiB available. Battery
91% → 85% (17.9 → 21.7 °C) for baseline/target and 81% → 76%
(20.4 → 22.8 °C) for stress. The tablet discharged throughout: this USB connection does
not power it. End-of-refresh `Process.WorkingSet64` samples: 244,023,296–332,685,312 bytes
(baseline/target), 189,071,360–237,322,240 bytes (stress). Offline working set is unavailable.

**First attempt aborted before any case.** Because the tablet is not powered over USB,
`stay_on_while_plugged_in` had no effect, and the screen timed out between readiness and the
matrix start. The runner's foreground guard refused all input. Under the plan's temporary
wake-support allowance, the screen timeout was raised from 120,000 ms to 1,800,000 ms for each run,
and both cleanups restored it. Records: `attempt-1-aborted/`, `wake-support*.json`.

**Stress continuation.** The capacity stop record (`cases/stress-entry-stop.json`) is retained
unchanged. After the developer freed space, the tablet briefly disappeared from USB entirely;
Windows listed no Samsung device until it was reconnected. Fresh readiness then verified identity,
APK, WebView, host artifact hashes, the retained target generation, the saved-work fingerprint
and note, and the stored target fixture. `stress-run.ps1` runs the same per-case functions as
`run-matrix.ps1`, copied verbatim, with its own host log, route, wake and cleanup records.

## Saved work, generations and searches

The retained pilot generation `ee25e3ce860d4f4fae65d191ff1252a5`, note **Samsung saved note
WI-032** and fingerprint `89e54f6dc96aa4bdef76ff5efc96146553352e8c2d513463ca85c236c4747af9` were verified before the first case.
Every report's before/after fingerprint matches the independently reconstructed canonical
work object. Each refresh produced a new generation from its predecessor across all three
profiles. Each offline open shows the latest successful refresh's generation. The stress data
and the edited work remain on the tablet.

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
| stress-r1 | `a177bf67-33d2-408d-8cf7-c6824bf8e058` | `4d8249c6880d47ccb8838d681e6c5bc1` | `14331ad8f74aa912e1562809f2a3de309f335ed1a0a2ab905b504b024510962a` |
| stress-r2 | `63a81937-e422-4358-9fd1-9a6b6fddaa1d` | `ff47a71f59254ffabec45a7e6e42cdd7` | `083cdc7bef70783900e748e8c685fe71607fc1445cea71677f14a6f3877d344d` |
| stress-r3 | `b53e5750-9348-4da2-ba99-c1e7b5a78fb6` | `58a6058d6662485fbbb8becaa83be90e` | `9d0da973f023dcd7a3455d43fa0643982909a47366a2985135de01cd9a3e03c6` |
| stress-r4 | `2be279da-0638-4fb4-abec-44f3a0884585` | `cc119f8e2e9b4e568d243f89ed43ebea` | `1223e7d0e2523775be74c910e608c4ffe9993c41b3b524ed3700791d5bc76f8a` |
| stress-r5 | `0d46a636-d1a9-4486-a460-d0ed44b3bdc7` | `f710c61654d94a6bb31189647e7b1858` | `40716a3d393594c741704f247a7b0ffabeb6d194a299e1b24beeb12ab2c2f39c` |
| stress-o1 | `cd612389-fbfc-4038-a51e-51c8c47f0169` | `f710c61654d94a6bb31189647e7b1858` | `f0648012e7bccb6cb2cd43105a913fda120b6564d6801aeefc928813b95afa79` |
| stress-o2 | `7638ef2c-b7bc-40bf-8703-5d9b97070afa` | `f710c61654d94a6bb31189647e7b1858` | `71ee32899de5e88da29ebffb2d37da957ab6f7f516d93c61140f3c1253778207` |
| stress-o3 | `308bbd8d-c147-4a31-852d-4609bb49f8cd` | `f710c61654d94a6bb31189647e7b1858` | `63a09db0f978d80e783adecf883f3477aa1c2f08de6966d89ecce80803013935` |
| stress-o4 | `a8c68f01-306d-455c-8f7c-26154e08d340` | `f710c61654d94a6bb31189647e7b1858` | `7eff90cf1bf32c39de9de272e6b0f897f3e071a18fe520507b5ed227821e02fc` |
| stress-o5 | `0692f558-fbf9-404a-bd52-f4ed38ba489f` | `f710c61654d94a6bb31189647e7b1858` | `7d00a692b4120f173ad35a9b1786eac77eb3590a9c4d6d2bca822acf5d54ca11` |

Each refresh ran the twenty fixed terms once, in order. Expected counts are computed
independently from the generator's six-name/six-town cycle at 500 or 2,000 Locations,
normalizing accents, case and apostrophes. All 300 counts agree. Query and observed-visible clocks
remain separate; observed-visible timing runs from the JS call to after render and is not
keystroke-to-photon.

## Scenario verification

| Agreed intent | Result / evidence |
|---|---|
| Keep the selected device and artifacts | Pass with the approved host deviation: same serial/model/OS/ABI/WebView, installed and archived APK, fixture and Unsent hashes; no app rebuild, reinstall, reset or other device. |
| Stop before a profile when headroom is insufficient | Pass: baseline and target entry passed 1 GiB. Stress stopped unstarted at 2,077,944 KiB &lt; 3 GiB without deleting data or shrinking fixtures. It ran only after the developer freed space and the guard passed at 12,482,340 KiB. |
| Keep every original attempt | Pass: 15 refreshes started and completed. Each has its own persisted start, outcome, report and verified new generation, plus manifest/hash/count checks. No failures; none replaced. |
| Keep edited work across profiles and reopens | Pass: canonical fingerprint/note and original Unsent identity persist through all 30 cases, including both profile transitions. |
| Return expected matches | Pass: 20 ordered terms per refresh and 300 independent count checks at 500/2,000 Locations, with separate query and visible clocks. |
| Reopen the recorded day offline and process-cold | Pass: 15 COLD launches with no route, no default network and no fixture request. Each showed the latest generation and saved work, with seven raw local-open points. |
| Record conditions and unknowns | Pass: per-profile counts, separate clocks, every proxy miss, numeric aggregates with denominators, point-sampled storage, unavailable offline memory, the aborted first attempt, the capacity stop and continuation, and the developer's “Smooth” at target and stress. |
| Restore only owned resources | Pass, twice: airplane 0 / Wi-Fi 1 / stay-on 0 / screen timeout 120,000 ms restored, owned route removed, host and wake helper verified exited, temporary UI dump copied then removed, APK and saved work retained. |

## Lifecycle and evidence

Each run started with readiness: it brought the spike forward (not measured), started the owned
host on `127.0.0.1:7206`, created the serial's USB reverse route and a bounded PC wake helper.
Each cleanup restored the recorded settings and stopped only those identified processes. Other
apps, devices and shared services were not operated.

Evidence: `.artifacts/wi032/samsung-repeat-session/`, including `summary.json`,
`validate-report.py`, the 30 original reports, per-case start/ready/observation/validation and
launch records, UI dumps, space points, both host logs, hashes, device conditions, both human
observations and both cleanup proofs. Unchanged-source verification remains **2,358 .NET / 34
JavaScript passes at `4998512`**; this measurement increment did not rebuild the app or repeat
the source suites.

Stop for Human Tight-Loop review. Remaining physical work: recovery, cancellation and
owner-isolation cases; the iPad mini/Mac check; then MI-02 and adoption 1C. No optimization, PR,
merge or main integration follows.
