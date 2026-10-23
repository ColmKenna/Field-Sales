# WI-032 — Samsung recovery, cancellation and owner isolation results (1B-B3)

## Outcome — 23 October 2026

The approved [1B-B3 plan](tablet-offline-samsung-recovery-plan.md) is **complete: all ten cases
passed**. Cancel during a real partial download and during a real partial import both kept the
previous whole generation and the edited work. A clean retry then completed. A package-only
force-close **before** activation reopened the whole previous generation; one **after**
activation reopened the whole new generation. A second synthetic owner started empty, got its
own data and work, and neither owner's data or note ever appeared under the other, across four
switches and a cold launch. **13 original reports**, each independently verified against an
expectation written before validation. No case was replaced or retried. WI-032 stays active;
the iPad check, MI-02 and adoption 1C remain.

| # | Case | Owner / profile | Action | Result |
|---|---|---|---|---|
| C1 | Cancel during download | 42 / baseline | Paused at **download** after the first written chunk (3.0 s after input), then Cancel | Report **cancelled** (`TaskCanceledException` in `httpTransferAndCompressedHash`); stress `f710c616…` stayed active and whole; note and fingerprint unchanged |
| C2 | Cancel during import | 42 / baseline | Paused at **import** after the first committed 128-entity batch (6.2 s), then Cancel | Report **cancelled** (`OperationCanceledException` in `decompressParseInsertAndProjectionIndexes`); same stress generation and work |
| C3 | Clean retry | 42 / baseline | Normal refresh, then twenty searches | **complete**: new baseline `f80559a9…`, full manifest checks, 20/20 search counts; total refresh 77.998 s |
| K1 | Force-close before activation | 42 / target | Paused at **before-activation** (38.9 s), package-only force-stop, one cold launch | Reopened C3's baseline `f80559a9…`, whole; interrupted target report **incomplete** with no generation |
| K2 | Force-close after activation | 42 / target | Paused at **after-activation** (54.4 s), force-stop, one cold launch | Reopened the new target `374c84cf…`, whole, matching the interrupted report's generation; that report stays **incomplete** |
| O1 | Switch to unused owner | 77 | Select spike-rep-77 | No downloaded data; no owner-42 note or fingerprint; report **incomplete** with no snapshot |
| O2 | Populate owner 77 | 77 / baseline | Download & apply, twenty searches | **complete**: owner-77 generation `f5b384b6…` from its own fixture; work matches unedited `unsent-77.json`; 20/20 counts |
| O3 | Return to owner 42 | 42 | Select spike-rep-42 | Target `374c84cf…`, edited note and fingerprint restored exactly |
| O4 | Owner 77 retained | 77, then 42 | Select 77, then 42 | Owner 77 still `f5b384b6…` with its own work; owner 42 still `374c84cf…` |
| O5 | Cold reopen as owner 42 | 42 | Force-stop, one cold launch | Owner 42 persisted; target `374c84cf…`, note and fingerprint; no fixture request |

## Observations, not established causes

- **Old-generation cleanup after a stress copy is expensive.** C3, a baseline refresh, took
  **77.998 s**, of which **69.724 s** was deleting the oldest
  retained completed generation, a stress copy from 1B-B2. Import itself took 6.464 s.
  Owner 77's first baseline refresh, with nothing to delete, took 8.630 s. In 1B-B2, stress
  refreshes deleted a stress generation in 15.3–28.1 s; why this deletion took longer is not
  established.
- **A force-close after activation leaves the older copies in place.** K2 stopped before
  old-generation deletion, and the next open did not resume it. Owner 42's sampled storage stayed
  at **2,009,890,152 bytes** from K2's reopen through O5, consistent with the stress,
  baseline and target copies all remaining (inferred from the retention rule and sampled size;
  the database was not inspected directly). The next successful refresh's retention rule would delete the
  extras. Production retention/cleanup design (WI-043/1C) should account for this.
- Cold-launch local opens after force-close: K1 673.4 ms, K2
  587.3 ms, O5 507.0 ms, all under the three-second proxy.
  Android activity clocks: 1,908 / 1,457 / 1,639 ms. Owner switches reopened in under 100 ms.

## Device, method and conditions

Same Samsung **SM-P610**, serial **R52T30AZZXZ**, Android 13/API 33, Arm64, WebView 155.0.8059.43;
installed Release schema-v4 APK `8b51486e7130e41e7ba9809e3b2f5b898a2b3d95c4e29b69616fa4d794964ec5`, verified at readiness and cleanup.
Spike source is unchanged since `4998512`. The fixture host is the developer-approved rebuild at
`5a05963`, re-verified against 1B-B2's recorded hashes. Fixtures for owner 42 baseline/target and owner 77
baseline, plus both Unsent files, were verified against their manifests. Every download used **USB adb
reverse** with **Simulate 10 Mbit/s + initial 100 ms**. Readiness confirmed the retained owner-42
stress generation `f710c61654d94a6bb31189647e7b1858`, the note and the fingerprint before any case.

The host logged exactly **13 fixture requests**: manifest and payload for C1, C2, C3, K1 and K2,
and manifest, Unsent and payload for owner 77's first refresh. That is **none** during any cold
reopen or owner switch. Free /data stayed about 11.07 GB (11,068,336–11,074,512 KiB).
The tablet is still not powered over USB; the screen timeout was raised to 30 minutes for the run
and restored.

**Interruption before C3 (controller bug, no case lost).** The first runner attempt stopped
while setting C3's selectors, before C3 started: the long “Test pause” list opened scrolled to its
current value, hiding “None”. The open list was closed with one Back after confirming it belonged
to the spike; nothing had been selected. The helper now scrolls the list until the option is
visible. The run resumed at C3; C1 and C2 kept their original evidence and were not repeated.
Records: `attempt-1-stopped/`, `cases.log`.

## Reports

Each report was pulled with matching device and local hashes. Interrupted refreshes were
recovered after the cold launch with **Export saved refresh**, the newest persisted refresh,
which the app showed by name.

| Report | Status | Owner | Profile | Generation | File | SHA-256 |
|---|---|---|---|---|---|---|
| C1-cancel-download | cancelled | spike-rep-42 | baseline | — | `tablet-spike-ad946aed-393b-42d0-921e-4f9c22e0c1bc.json` | `d3e0f6c87913b614661f801341e83d43c1060a04a51d0627be8fd63d3949bf13` |
| C2-cancel-import | cancelled | spike-rep-42 | baseline | — | `tablet-spike-ac01125e-b56e-45f9-8ce0-55c90f416590.json` | `dedd877064ef6aa973bc51631aeca27e8632ceb9c57b1d97434ab5e5e703eed6` |
| C3-retry | complete | spike-rep-42 | baseline | `f80559a9cde146c6a58480659ede99e3` | `tablet-spike-cbf5f8e0-87b4-45b0-92fa-cc0124e9093d.json` | `aac92baad98c3c8948a66862980815a959ba0804f97cebaf4c7467cbe8aad339` |
| K1-before-activation-interrupted | incomplete | spike-rep-42 | target | — | `report-c5f69899-41e3-4192-8af1-76e41c1e54ea.json` | `da24f4da7338d3613c044b4b29309ba149e2504dc5714107d1fcc324fde52d2c` |
| K1-before-activation-reopen | offline-open-complete | spike-rep-42 | baseline | `f80559a9cde146c6a58480659ede99e3` | `tablet-spike-113d42fe-3277-467a-9283-6145effafb21.json` | `9f529ed34704f608e5673e873d02fb3dd11c5e2ae4aaa000f0af8159377d63f4` |
| K2-after-activation-interrupted | incomplete | spike-rep-42 | target | `374c84cf43e24ee9bfea39e3757c1e30` | `report-614dfdbf-6c89-42ee-9199-8932797f038f.json` | `e27ba0bc34f25c8b4fbf57b8cd789f6a6523062745c52bbbec98ce238226decd` |
| K2-after-activation-reopen | offline-open-complete | spike-rep-42 | target | `374c84cf43e24ee9bfea39e3757c1e30` | `tablet-spike-37f03c7f-24ef-42d9-a77b-d47a363e526a.json` | `c9988f8d17236248b1c4faab56e3474dc4d694ab9fe018d7866c0be607a0b10e` |
| O1-switch-to-77 | incomplete | spike-rep-77 | — | — | `tablet-spike-e5ca441f-431b-458e-a0bb-899dd7a91bd9.json` | `1b7b5102aa11afecbd4fcdb3dc90c36e6ce2176da7ab19c8e6008ee53189f53c` |
| O2-populate-77 | complete | spike-rep-77 | baseline | `f5b384b6b1d8478e9b354be1e7e1f698` | `tablet-spike-daffe5d9-bae6-4ffe-8b76-60cae58c205a.json` | `5800be8523b21f5cacf866924d5a420ff7fb5d8e31e61fea4de20fd42e3866e5` |
| O3-return-to-42 | offline-open-complete | spike-rep-42 | target | `374c84cf43e24ee9bfea39e3757c1e30` | `tablet-spike-302bed67-7827-428d-b039-cf10a079b33e.json` | `9b34f19af6376defdad4a5d8cf5eb2c8db55c128c9643dbb466317046d712c46` |
| O4a-switch-to-77-again | offline-open-complete | spike-rep-77 | baseline | `f5b384b6b1d8478e9b354be1e7e1f698` | `tablet-spike-25942272-fc6d-435e-b0a2-45449ee7563d.json` | `744dba2d9c918901903c446d0a91c9f8603ea64fd026114cc44725a08fcaf3b8` |
| O4b-return-to-42-again | offline-open-complete | spike-rep-42 | target | `374c84cf43e24ee9bfea39e3757c1e30` | `tablet-spike-c9e2b3db-9a2d-4d7a-89bd-7eab390c5834.json` | `45461e903fee98d922488f9760593ef085f405bf53ceba6a25c232fc51ba5aec` |
| O5-cold-reopen-42 | offline-open-complete | spike-rep-42 | target | `374c84cf43e24ee9bfea39e3757c1e30` | `tablet-spike-e76a50c4-2d5f-4394-83a8-857761c201da.json` | `c4febee0606a49de9c5145345733d57355c4337370141f36dd47c45fe453dbb2` |

| Report with a local open | Local open ms |
|---|---:|
| C1-cancel-download | 39.1 |
| C2-cancel-import | 26.5 |
| C3-retry | 33.4 |
| K1-before-activation-reopen | 673.4 |
| K2-after-activation-reopen | 587.3 |
| O1-switch-to-77 | 82.6 |
| O2-populate-77 | 17.3 |
| O3-return-to-42 | 43.7 |
| O4a-switch-to-77-again | 40.9 |
| O4b-return-to-42-again | 39.2 |
| O5-cold-reopen-42 | 507.0 |

## Scenario verification

| Agreed intent | Result / evidence |
|---|---|
| Should_KeepActiveDataAndUnsent_When_TransferIsCancelled | Pass: C1 paused after a real written chunk, cancelled, previous stress generation and work unchanged, report cancelled. |
| Should_KeepActiveDataAndUnsent_When_ImportIsCancelled | Pass: C2 paused after a committed batch in the staged generation, cancelled; same generation and work, and staged rows never visible. |
| Should_ImportCleanly_When_RetryingAfterCancellation | Pass: C3 completed with full manifest/hash/count checks and 20/20 search counts. |
| Should_RecoverTheOldGeneration_When_ProcessEndsBeforeActivation | Pass: K1 reopened the whole previous baseline; interrupted report incomplete with no generation. |
| Should_RecoverTheNewGeneration_When_ProcessEndsAfterActivation | Pass: K2 reopened the whole new target, matching the interrupted report's generation. |
| Should_IsolateSnapshotAndWork_When_SelectingAnotherSyntheticOwner | Pass: O1–O5; owner 77 empty, then its own generation and work; no cross-owner data or note through switches and a cold launch. |
| Should_KeepEveryOriginalReport_When_ARunIsCancelledOrInterrupted | Pass: 13 unique reports with raw cancelled/incomplete/complete statuses; nothing replaced; the controller stop is recorded. |
| Should_RestoreOnlyOwnedResources_When_TheIncrementEnds | Pass: airplane 0 / Wi-Fi 1 / stay-on 0 / screen timeout 120000 ms restored; route removed; host and wake helper verified exited; APK and both owners' data retained, owner 42 selected. |

## Evidence and next step

Evidence: `.artifacts/wi032/samsung-recovery-session/`, including `validate.py`, per-case start,
paused, outcome, expectation and validation records, launch records, UI dumps, the 13 original
reports, the host log, device conditions and cleanup proof. No application or test source changed.
Source verification remains **2,358 .NET / 34 JavaScript passes at `4998512`**.

**Owner 77's synthetic data remains on the tablet**, as planned. Stop for Human Tight-Loop
review. Remaining: the iPad mini/Mac check, then MI-02 and adoption 1C. No optimization, PR, merge or
main integration follows.
