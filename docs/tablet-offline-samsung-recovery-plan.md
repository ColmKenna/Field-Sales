# WI-032 — Samsung recovery, cancellation and owner isolation (1B-B3)

## Selection and approval boundary

Resume **WI-032 — Sign in on the tablet and download the day's data offline**, T-4.1.1,
REP-US-001 / IAM-US-003. It is the sole active item: Human Tight-Loop, Human-Led tests, Complex,
High risk, Low confidence. Dependencies WI-002, WI-021 and WI-005 are done. Status stays in
`workItemStatus` in `plan_docs/field-sales-delivery/plan-data.js`; WI-032 remains active and the
console is not edited.

The developer's **proceed** after the complete 1B-B2 checkpoint (`97dcf4f`) accepts it and
continues to the next physical increment. The [parent device plan](tablet-offline-device-spike-plan.md)
names four scenarios whose device evidence belongs on the Samsung and has not been collected
there: **Samsung Cancel**, **device force-close before and after activation**, and **a second
synthetic owner**. The emulator covered cancel-during-download, both force-closes and switching
to an *unused* owner 77 once; a *populated* second owner was never downloaded on any device.
This document makes that increment concrete. It awaits **Approved** before any device action.

## Starting state

At the end of 1B-B2 the Samsung (**R52T30AZZXZ**, SM-P610, Android 13/API 33, Arm64, WebView
155.0.8059.43) held:

- installed Release schema-v4 APK SHA-256 `8b51486e7130e41e7ba9809e3b2f5b898a2b3d95c4e29b69616fa4d794964ec5`
- owner `spike-rep-42` with active **stress** generation `f710c61654d94a6bb31189647e7b1858`
- the edited note **Samsung saved note WI-032**, fingerprint
  `89e54f6dc96aa4bdef76ff5efc96146553352e8c2d513463ca85c236c4747af9`
- about **11.06 GB** free on /data; owner 77 never selected or downloaded on this device.

Readiness re-verifies all of these and the fixture host's recorded hashes (the developer-approved
host rebuilt at `5a05963`). Owner 77's baseline fixture (`final-profiles/77/baseline`, 1,569,515
gzip bytes) and `unsent-77.json` are verified against their manifest. No rebuild, reinstall,
reset, data clearing or source-suite repeat.

## How the cases work in this app

- **download** pauses after the first chunk of the payload is written and flushed, so it is a
  real partial transfer. **import** pauses after the first 128-entity batch is committed to the
  staged generation, so it is real partial insertion. A pause holds until **Cancel** or a
  force-close; the controller cannot miss it.
- **before-activation** pauses after full hash/count verification, before the single activation
  transaction. **after-activation** pauses after that transaction commits, before old-generation
  deletion.
- The app keeps the active and the immediately preceding completed generation. Switching
  **Synthetic owner** reopens that owner's separate snapshot and Unsent databases.

## One increment after approval

Same method as 1B-B2: a new ignored session `.artifacts/wi032/samsung-recovery-session/`,
controllers adapted from the repeat session's verified helpers, USB adb reverse, **Simulate
10 Mbit/s + initial 100 ms**, explicit serial on every command, foreground guard before every input,
and each case's start persisted before its action. The temporary 30-minute screen timeout is set
first, because the tablet is not powered over USB, and restored at cleanup.

| # | Case | Owner / profile | Action | Expected outcome |
|---|---|---|---|---|
| C1 | Cancel during download | 42 / baseline | Pause **download**, wait for “Paused at download”, tap **Cancel** | Status “Cancelled…”; report **cancelled**; active stays stress `f710c616…`, whole; fingerprint unchanged |
| C2 | Cancel during import | 42 / baseline | Pause **import**, wait, tap **Cancel** | Same as C1, after real partial insertion |
| C3 | Clean retry | 42 / baseline | Pause **None**, Download & apply, twenty searches | New baseline generation, whole: manifest/hash/counts, 20 counts match the 500-Location oracle, fingerprint unchanged |
| K1 | Force-close before activation | 42 / target | Pause **before-activation**, force-stop only the spike, one cold launch | Reopen shows C3's baseline generation, whole; interrupted report **incomplete**; no fixture request during reopen |
| K2 | Force-close after activation | 42 / target | Pause **after-activation**, force-stop, one cold launch | Reopen shows the new target generation, whole; interrupted report **incomplete**; fingerprint unchanged |
| O1 | Switch to unused owner | 77 | Select **spike-rep-77** | No downloaded data; no owner-42 note or fingerprint visible |
| O2 | Populate owner 77 | 77 / baseline | Download & apply, twenty searches | Owner-77 generation from its own fixture; owner-77 work matches the unedited `unsent-77.json` fingerprint |
| O3 | Return to owner 42 | 42 | Select **spike-rep-42** | K2's target generation, edited note and fingerprint restored exactly |
| O4 | Owner 77 retained | 77, then 42 | Select 77, then 42 again | Owner 77's generation and work unchanged; owner 42 unchanged |
| O5 | Cold reopen as owner 42 | 42 | Force-stop, one cold launch | Owner 42 persisted; K2's generation, note and fingerprint; no fixture request |

After each force-close, the controller exports the cold reopen's report, then the persisted
interrupted refresh with **Export saved refresh**. Every report is pulled with a matching device
and local hash and checked by an independent validator. The validator is extended for
cancelled/incomplete statuses, owner 77's manifest and Unsent fingerprint, and the generation
expected at each step. Bounds stay as before: 900 s per refresh, 45 s to first observed readiness,
90 s per twenty-term batch, 600 s per export, plus **120 s to reach a requested pause**.

Any unexpected outcome stops new actions: a wrong generation, a partial snapshot, a changed
fingerprint, a missing or extra report, an unexpected foreground, a USB loss or a cutoff. The
original evidence is kept, and nothing is repeated to obtain a different result. Capacity guards
stay at 1 GiB free before entering and 512 MiB before each download; about 11 GB is available.

Cleanup restores the recorded network, wake and screen-timeout state, removes only the owned
route, and stops only the identified host and wake helper. It leaves the app selected on owner
42. **Owner 77's synthetic data stays on the tablet**, because no data deletion is authorized.
Then results, the device-results index entry, a checkpoint here and the notes are committed and
pushed, and the increment **stops for review**.

## Human-Led scenario intents for approval

These specialize the parent plan's approved scenarios for the physical Samsung. No application or
automated-test source change is proposed.

| Scenario | Required evidence |
|---|---|
| Should_KeepActiveDataAndUnsent_When_TransferIsCancelled | C1: real partial transfer, Cancel, report cancelled, previous whole generation and edited work unchanged. |
| Should_KeepActiveDataAndUnsent_When_ImportIsCancelled | C2: real partial insertion, Cancel, staged rows never visible; same generation and work. |
| Should_ImportCleanly_When_RetryingAfterCancellation | C3: retry completes, full manifest checks, 20 search counts consistent with a whole baseline, no leftovers mixed in. |
| Should_RecoverTheOldGeneration_When_ProcessEndsBeforeActivation | K1: package-only force-close, one cold launch, whole previous generation, work unchanged, interrupted report kept as incomplete. |
| Should_RecoverTheNewGeneration_When_ProcessEndsAfterActivation | K2: whole new generation after force-close, work unchanged, interrupted report incomplete. |
| Should_IsolateSnapshotAndWork_When_SelectingAnotherSyntheticOwner | O1–O5: owner 77 starts empty, gets its own generation and work, and neither owner's data or note appears under the other, across switches and a cold launch. |
| Should_KeepEveryOriginalReport_When_ARunIsCancelledOrInterrupted | Every initiated case has its own report(s) with raw status retained; no report replaced or retried. |
| Should_RestoreOnlyOwnedResources_When_TheIncrementEnds | Settings, route, host and wake helper restored or stopped by identity; APK and both owners' data retained; owner 42 selected. |

## Risks, exclusions and missing information

Risks **(inferred)**: the USB connection has dropped before and does not power the tablet. A
loss during a pause leaves the app paused, which is harmless, but it stops the increment and
needs a fresh readiness check. K2 is killed before old-generation deletion, so an extra retained
generation may stay until the next refresh's cleanup. That is recorded as storage, not judged
pass or fail. Force-close is package-only and does not certify power loss.

Out of scope: deliberate storage or commit fault injection on the device
(`Should_KeepPreviousData_When_StorageOrCommitFails` stays covered by integration tests; any device
failure would be reported); the post-activation `pending-*` cleanup boundaries; recovery at stress
scale; repeating offline batches already covered by 1B-B2; the iPad check; 1C adoption; production
sign-in/Sync; PR or main integration.

Missing information: none. The human must keep the tablet connected, unlocked and untouched
during the run. A cable or port that also charges the tablet would make the run more reliable.
This scope, the ten cases and the eight scenario intents await **Approved**.

## Execution checkpoint — 23 October 2026

The developer replied **Approved** and re-authorized USB debugging. All ten cases passed. The first
runner attempt stopped before C3 started because a scrolled dropdown hid "None"; the helper was
fixed and the run resumed at C3 without repeating C1 or C2. Results:
[tablet-offline-samsung-recovery-results.md](tablet-offline-samsung-recovery-results.md). Stop for review.
