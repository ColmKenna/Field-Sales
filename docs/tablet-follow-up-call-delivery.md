# WI-056 — Record a follow-up call linked to the original

T-5.7.1 / REP-US-016 S1, S2 and edge cases. Feature branch: `feature/wi-056-record-follow-up-call-linked`.

What a rep forgot, or did later, is recorded as a new Call that names the one it follows up. The original is never rewritten.
The latest reading of each product becomes its current stock level, and both readings stay on their own Calls.

## Scope and approved decisions

- The developer answered three questions and replied **Approved** to D1–D8 and the 14-scenario list, then **Apply**.
- At the checkpoint (the link and the stock-level function, with scenarios 2–4 passing) the developer replied `Continue T-5.7.1`.

| # | Decision |
|---|---|
| D1 | The link: `TabletCall` gains optional `followUpOf`, a section version 1 of the Call contract already reserved. The server stores it in `ReceivedCall.FollowUpOf`, added by migration `AddFollowUpOfToReceivedCalls` (one nullable column, nothing to backfill). The server refuses an empty link or a link to itself. The link isn't a foreign key, because the original may arrive later in the same Sync. |
| D2 | Follow-up mode: Record call at `/locations/{id}/calls/{callId}/follow-up` reads "Follow-up call" and "Following up your call on Tue 22 Sep". It has its own channel (none chosen) and date. The stock section starts empty: Add product in person, "Filter or search" on Phone. Notes, Add to order and Open order work as for a new Call. `CallStore.Save(..., followUpOf)` refuses a link to a Call this tablet doesn't hold at the same shop. After saving, the rep returns to the shop, which reads "Call saved". The draft is kept apart, so a Call being recorded or corrected at the shop isn't lost. |
| D3 | Entry points: **Record a follow-up call** on Edit Call and on the call page (unsent, being sent or sent) opens the follow-up for that Call. A follow-up of a follow-up links to the Call it was opened from. |
| D4 | Current stock level: `StockReadings.Current` in the shared call contracts. For each product, the count on the Call saved latest, compared as an instant; upload order, receive order and the date the Call names never decide it. Equal moments go to the larger Call id. Only counts are readings: Out (a count of 0) is one; Not checked and Low without a count aren't. Both readings stay. No screen uses it yet. |
| D5 | Suggested List: the last check is the newest In-person Call that isn't a follow-up and has stock entries, plus every product on its follow-ups and theirs (any channel, same shop). A follow-up never becomes the last check itself. No snapshot contract change. |
| D6 | A follow-up to a sent Call is a new Unsent Call. |
| D7 | Out of scope: Visit Due completion (S3, E13), campaign outcomes later (E20), showing the stock level on a screen, reaching sent Calls from the shop (WI-075). |
| D8 | Finish: push, PR, merge after CI (merge commit, branch kept), update local main. |

## The checkpoint question

"Does 'current stock level' pick the latest reading correctly when calls are recorded out of order?" Yes. Scenario 3 shows it:

- A Call saved at 08:30 on a tablet set to UTC beats one saved at 09:10 Irish summer time (08:10 UTC), although its clock reads
  earlier.
- A follow-up saved the evening before doesn't beat the morning's counts.
- The answer is the same whatever order the Calls are listed in.

## How it fits together

- **Tablet.** `CallStore.Save` takes the optional link and checks it after the snapshot check. A correction (`CallStore.Correct`)
  copies the saved Call with `with`, so it keeps the link.
- **Drafts.** `CallDrafts` gains a follow-up slot beside the shop's draft and Edit Call's. Order entry opened from a follow-up finds
  its draft through `ReadCall`/`KeepCall`, and its Back to call returns to the follow-up (`OriginalOf`).
- **Server.** `TabletSnapshotBuilder.ReadLastStockChecksAsync` picks the last non-follow-up In-person check per shop. It then follows
  the links outward one generation at a time, keeping a follow-up only when it is at the same shop as the Call it names. A Call
  already in a check is never visited twice, so a loop in the links can't hang it.
- **Amended on purpose.** WI-055 scenario 8, `Should_OpenRecordCallAtTheShop_When_RecordAFollowUpCallIsTapped`, expected the
  follow-up to open a plain new Call. It is now WI-056 scenario 6 and expects the follow-up route.

## Agreed scenarios

| # | Scenario | Evidence |
|---|---|---|
| 1 | S1: a new Call, linked, with its own date and channel; the original untouched | `FollowUpCallTests.Should_SaveANewCallLinkedToTheOriginalWithItsOwnDateAndChannel_When_AFollowUpCountsNasalSpray15mlAs2ByPhone` |
| 2 | S2: 13 at 16:00 becomes current over 3 at 09:40; both readings stay | `StockReadingsTests.Should_Make13TheCurrentStockLevelAndKeepBothReadings_When_AFollowUpAt1600Counts13OverThe0940CountOf3` |
| 3 | The checkpoint question: out of order, the later save wins | `StockReadingsTests.Should_PickTheLatestSavedReading_When_CallsAreRecordedOutOfOrder` |
| 4 | D4: only counts are readings | `StockReadingsTests.Should_IgnoreNotCheckedAndLowOnlyEntries_When_WorkingOutTheCurrentStockLevel` |
| 5 | D6: a follow-up to a sent Call is a new Unsent Call | `FollowUpCallTests.Should_SaveTheFollowUpAsANewUnsentCall_When_TheOriginalWasSent` |
| 6 | D3: from Edit Call (amends WI-055 scenario 8) | `CallCorrectionScreenTests.Should_OpenTheFollowUpForThatCall_When_RecordAFollowUpCallIsTappedInEditCall` |
| 7 | D3: from a sent Call's page | `FollowUpCallScreenTests.Should_OpenTheFollowUpForThatCall_When_RecordAFollowUpCallIsTappedOnASentCall` |
| 8 | D2: no stock rows, no channel, the original named | `FollowUpCallScreenTests.Should_StartWithNoStockRowsAndNameTheOriginal_When_AFollowUpCallOpens` |
| 9 | D2: the screen saves the link and returns to the shop | `FollowUpCallScreenTests.Should_RecordTheFollowUpLink_When_AFollowUpIsSavedOnTheScreen` |
| 10 | Storage: a link to a Call not held, at another shop, or to itself is refused | `FollowUpCallTests.Should_RefuseTheFollowUp_When_TheOriginalIsNotACallAtThisShopOnThisTablet` (3 cases) |
| 11 | D2: a Call being recorded at the shop is kept, whether the follow-up is left or saved | `FollowUpCallScreenTests.Should_KeepACallBeingRecordedAtTheShop_When_AFollowUpIsStartedThere` |
| 12 | Server: the link is stored; an empty or self link is refused | `SuggestedListTests.Should_StoreTheFollowUpLink_When_TheServerReceivesAFollowUpCall` |
| 13 | D5: a follow-up's products join the last check, transitively; a follow-up never becomes it | `SuggestedListTests.Should_AddTheFollowUpsProductsToTheLastStockCheck_When_AnInPersonFollowUpCountsAForgottenProduct` |
| 14 | End to end: both upload in one Sync, the link survives, the next list carries both | `SuggestedListTests.Should_KeepTheFollowUpLinkedToItsOriginal_When_ASyncUploadsBoth` |

Mutation checks:

| Mutation | Tests that failed |
|---|---|
| Local clock time compared instead of the instant | 3 |
| Not checked treated as a count of 0 | 4 |
| The first Call listed wins | 2, 3, 4 |
| A follow-up can become the last check | 13, 14 |
| Follow-ups joined one generation only | 13 |
| The screen saves a follow-up without its link | 9, 11 |
| A follow-up starts from the Suggested List | 8, 11 |
| A follow-up's draft written to the shop's slot | 11 |

## Verification

- `dotnet build FieldSales.slnx -c Release --no-incremental`: no warnings.
- Full suite (SQL Server): 3,280 passed, 0 failed, including Tablet.Components 206, Tablet.Core 341, Tablet.Core unit 42 and
  Api 206.
- `node --test plan_docs/field-sales-delivery/tests/status-store.test.js`: 11 passed.
- Android Release build (`scripts/tablet-app.ps1 -Action Build -Configuration Release`): succeeded.

## Not done here

Showing the current stock level on a screen, Visit Due completion (S3), campaign outcomes (E20) and reaching sent Calls from the shop
(WI-075). Order entry opened from a follow-up (its Low tab and Back to call) is built but not covered by an agreed scenario. Not
checked on a device.
