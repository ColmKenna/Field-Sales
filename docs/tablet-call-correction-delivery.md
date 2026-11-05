# WI-055 — Correct a saved call before it syncs

T-5.6.1 / REP-US-015 S1–S4 and edge cases; design decision "Corrections constrained by the interface". Feature branch:
`feature/wi-055-correct-saved-call-before-it`.

Before Sync, a rep can correct a saved Call: change its channel, counts, Low marks and notes, and remove lines. Nothing can be
added. The screen offers **Record a follow-up call** where adding would be. Once the Call is sent it is read-only.

## Scope and approved decisions

- The developer answered three questions and replied **Approved** to D1–D8 and the 17-scenario list, then **Apply**.
- At the checkpoint (characterisation passing) the developer replied `Continue T-5.6.1`.
- During step 2 the developer chose "Hash after the claim" for the byte-hash tests (below).

| # | Decision |
|---|---|
| D1 | The call page: a read-only page at `/locations/{id}/calls/{callId}` showing the channel and date, standing, summary, notes and stock lines. Sync's Unsent review of a call opens it with **Open call**. An unsent call offers **Edit call** and **Record a follow-up call**; a sent call offers the follow-up only (S4). Sent calls get a way in with WI-075. |
| D2 | Edit Call is Record call in an edit mode at `/locations/{id}/calls/{callId}/edit`. The rep can change the channel, change a counted line's count (but not clear it; Remove drops the line), tick or untick Low, use Out on a phone row that already has a count, edit or clear notes the call already had, and Remove any line. There is no Add product, catalogue search, category browsing, Add count, count on a Not checked line, Pitch notes opener when the call had none, Add to order, Open order or Delete. |
| D3 | **Record a follow-up call** stands where Add product is, and where Pitch notes would be when the call had none. It opens Record call at the shop for a new call; WI-056 adds the link to the original. |
| D4 | Save opens a review dialog, "Save changes?", with the summary. `CallStore.Correct` applies the same checks and also enforces D2's rules. It keeps the call's id, date, save time, rep, snapshot and place in the Unsent queue. It refuses a call that is sent, being sent, refused, or for another shop. After saving, the rep returns to the call page, which reads "Changes saved." |
| D5 | Sync now claims a call before sending it, as it already did for orders. A call being sent reads "This call is being sent. Sync again before changing it." and offers no Edit call. Needs Attention calls keep WI-042's handling. |
| D6 | Unsaved edits survive the app resuming, kept separately from any call being recorded at the same shop. Back with changes asks "Leave without saving these changes?". Leaving keeps the saved call unchanged. Edit Call never touches an order (T7.8). |
| D7 | Out of scope: the follow-up link (WI-056), the sent-call view and its entry point (WI-075), website corrections (WI-120 / E13), competitor notes (T-5.8.1, not built yet, so there's nothing to edit), Visit Due completion (E13), and Needs Attention calls. |
| D8 | Finish: push, PR, merge after CI (merge commit, branch kept), update local main. |

## Characterisation (checkpoint), passing before and after

- C1 (new): `Should_ReadCallRecordedWithItsChannel_When_ASavedCallIsReviewedFromSync`.
- C2 (existing): `CallRecordingTests.Should_SaveOnce_When_SaveIsTappedTwice`. One call, unchanged.
- C3 (existing): `CallRecordingTests.Should_StayUnsentThenUploadAndMarkSent_When_TheRepSyncs`. Sent exactly as saved.
- C2 and C3 already existed, so they are relied on rather than duplicated.

## Amendment: byte-hash tests

Claiming calls (D5) writes an `upload_claims` row during a Sync's upload. The test server answers uploads with 503, which means
"the server may hold it", so the claim stays. Nine Tablet.Core cases hash the Unsent database files to prove "a download never
opens the Unsent database", and those hashes changed. The developer chose "Hash after the claim":

- The tests take their hash after a first Sync has claimed the calls (`TestTablet.SaveClaimedWorkAsync`).
- Later Syncs' claims change nothing, so the strict byte-for-byte check on downloads stays.

The changed tests are:
- `TabletStorageScenarios.Should_NeverAlterUnsentWork_When_ASnapshotIsDownloaded`;
- `LocationReadCharacterizationTests.Should_PreserveAssignedSearchAndSavedWork_When_SnapshotIsReplaced`;
- `LocationContactStatusTests.Should_ReadContactStatusOffline_When_DownloadIsReopenedAndRefreshed`;
- `LocationContactStatusTests.Should_KeepPreviousContactAndWork_When_ContactStatusDownloadFails` (6 cases).

## Agreed scenarios

| # | Scenario | Evidence |
|---|---|---|
| 1 | S1: a count of 3 changed to 13 saves 13 | `CallCorrectionScreenTests.Should_SaveTheCountAs13_When_ColdFluRelief16sCountedAs3IsChangedTo13InEditCall` |
| 2 | S2: channel, notes, counts and Low marks change | `Should_SaveTheChangedChannelNotesCountsAndLowMarks_When_EditCallIsSaved` |
| 3 | S2: no way to add, on either channel (the checkpoint question) | `Should_OfferNoWayToAddContent_When_EditCallIsOpened` |
| 4 | S2: the follow-up stands in place of adding | `Should_OfferRecordAFollowUpCallInPlaceOfAdd_When_EditCallIsOpened` |
| 5 | S3: Vitamin D removed | `Should_RemoveTheLine_When_VitaminD1000IU90sCountedByMistakeIsRemovedAndSaved` |
| 6 | S4: a sent call offers no Edit call, only the follow-up | `Should_OfferRecordAFollowUpCallAndNoEditCall_When_ASentCallIsOpened` |
| 7 | D1: Open call from Sync, then Edit call | `Should_OfferEditCall_When_AnUnsentCallIsOpenedFromSync` |
| 8 | D3: the follow-up opens Record call at the shop | `Should_OpenRecordCallAtTheShop_When_RecordAFollowUpCallIsTapped` |
| 9 | Edge case: no Delete | `Should_OfferNoDelete_When_EditCallIsOpened` |
| 10 | T7.8: unticking Low never touches the order | `Should_LeaveTheOrderLineUnchanged_When_LowIsUntickedInEditCall` |
| 11 | D6: leaving keeps the saved call unchanged | `Should_KeepTheSavedCallUnchanged_When_EditCallIsLeftWithoutSaving` |
| 12 | D2: switching to Phone keeps only what was mentioned | `Should_SaveOnlyMentionedRows_When_EditCallSwitchesTheChannelToPhone` |
| 13 | Storage: nothing added, no count cleared | `CallCorrectionTests.Should_RefuseTheCorrection_When_ItAddsAProductACountOrNotesOrClearsACount` (4 cases) |
| 14 | Storage: being sent, sent, refused, another shop | `Should_RefuseTheCorrection_When_TheCallIsBeingSentOrHasBeenSent` (4 cases) |
| 15 | D5: claimed before sending; a correction during the upload is refused | `Should_ClaimTheCallBeforeSendingIt_When_SyncUploadsACall` |
| 16 | D4: id, date, save time, rep, snapshot and queue place kept | `Should_KeepTheCallsIdDateAndSavedTime_When_ACorrectionIsSaved` |
| 17 | End to end: the server receives the corrected call | `SuggestedListTests.Should_SendTheCorrectedCall_When_AnEditedCallIsSynced` |

Mutation checks:

| Mutation | Tests that failed |
|---|---|
| Calls not claimed on upload | 15 |
| The additions rule switched off in storage | 13 (all 4 cases) |
| Add product shown in Edit Call | 3 |
| A sent call correctable | 6 |
| A count box on a Not checked row | 3 |

## Verification

- `dotnet build FieldSales.slnx -c Release --no-incremental`: no warnings.
- Full suite (SQL Server): 3,265 passed, 0 failed, including Tablet.Components 202, Tablet.Core 336 and Api 203.
- `node --test plan_docs/field-sales-delivery/tests/status-store.test.js`: 11 passed.
- Android Release build (`scripts/tablet-app.ps1 -Action Build -Configuration Release`): succeeded.

## Not done here

The follow-up link (WI-056), the sent-call view and its entry point (WI-075), website corrections (WI-120). Not checked on a device.
Commit `77d9d11` (call page and Edit Call) doesn't build the component test project by itself: its test adapter landed in the
next commit, `514e21b`. Every later commit builds.
