# WI-046 — Edit, reopen or delete an unsent order

T-5.2.1 / REP-US-013. Feature branch: `feature/wi-046-edit-reopen-delete-unsent-order`.

Before Sync, a rep can change a line's quantity, remove a line, reopen a Ready to Send order or delete an order. Once an
order's upload has started, and once it is Sent, none of these are offered or accepted.

## Scope and approved decisions

The developer replied **Approved** to the plan, decisions D1–D6 and the 18-scenario list, then **Apply**. After the
transition-table checkpoint they lifted the remaining stops and asked for the branch to be pushed, a PR opened and merged
when complete, and local `main` brought up to date with the remote.

| # | Decision |
|---|---|
| D1 | Additive local migration: Unsent database format 4 adds `upload_claims`. No server, endpoint, contract or snapshot change. |
| D2 | A Needs Attention order cannot be reopened or edited. WI-042's Open, re-mark and Delete remain. |
| D3 | Deleting an order removes only the order; a saved Call stays and still uploads (WI-047 decides any Call-side display). |
| D4 | At a shop no longer assigned: reopen, change, remove and delete are allowed; adding a product still needs assignment. |
| D5 | A line whose product is missing from today's snapshot can be removed, but its quantity can't change. |
| D6 | A quantity change keeps the captured price, unit and snapshot (quantity breaks are E6). |

## Transition table (`OrderTransitions`)

| From | Action | To |
|---|---|---|
| New (nothing saved) | Add a line | In Progress |
| In Progress | Add a line, change quantity, remove a line | In Progress (the last line goes only with the order, after confirmation) |
| In Progress | Mark Ready to Send | Ready to Send |
| In Progress | Delete order (confirmed) | Deleted, nothing sent |
| Ready to Send | Reopen | In Progress |
| Ready to Send | Delete order (confirmed) | Deleted, nothing sent |

Everything else is refused. Sending (claimed by an upload): "Sending this order wasn't confirmed. Sync again before
changing it." Sent, Needs Attention or unreadable: "This order can't be changed here." Ready to Send, for adding, changing
or removing: "This order is ready to send." (reopen first). Where an order stands is read with a claim first, then a
technical refusal, then its saved state.

## The upload claim (criterion 6, and WI-039 D1's open question)

`WorkUploader` claims each Ready to Send order just before sending it, holding the same per-rep gate every order change
holds (`OrderStore.GateFor`), and only if the saved body is still the body it queued. A change either lands before the
claim (the order is skipped and not counted as "not sent") or is refused after it.

| Upload answer | Claim |
|---|---|
| Confirmed (`Received` / `AlreadyReceived`) | Gone with the move to Sent |
| Technical refusal (400 / 409 / 413) | Cleared in the same transaction as Needs Attention |
| 401 / 403, or no connection made (`HttpRequestError` NameResolution, Connection, SecureConnection) | Cleared: the server kept nothing |
| Dropped mid-request, timeout, 5xx, unreadable receipt | **Kept**: the order stays locked until a later Sync gets an answer |

So an id the server may hold is never re-sent with a changed body, and never deleted. The claim lives in its own table,
not on `UnsentItem`, so captured work compares equal whether claimed or not. It covers orders only, because Calls can't be
changed by the rep yet; **WI-055 (Call corrections) should extend it to Calls.** One consequence: a Sync whose upload of a
Ready to Send order fails without a definite answer now writes the claim to the Unsent database.

## Screen

`NewOrder.razor` also serves `/locations/{shop}/orders/{order}`. In Progress lines get **Change** (an inline field
opened with the current quantity, with the stepper for measured products) and **Remove**; the footer gets **Delete
order**. Ready to Send shows its notice with **Reopen** and **Delete order**. A claimed, Sent or Needs Attention order
shows a notice and **Close** only. Delete asks "Delete this order? It hasn't been sent. Deleting removes it from this
tablet." (**Delete order** / **Keep order**); removing the last line asks "Remove the last line and delete this order?"
(**Remove and delete** / **Keep line**). The Unsent review on Sync offers **Open order** for orders not in Needs Attention;
the list rows stay button-free (T1.1).

## Acceptance criteria

| # | Criterion | Evidence |
|---|---|---|
| 1 | Changing "Cold & Flu Relief 16s" from 24 to 36 on an In Progress order and saving shows 36 | `OrderEditTests` and `NewOrderComponentTests` `Should_SaveAndShow36_When_ColdFluRelief16sOnAnInProgressOrderIsChangedFrom24To36` |
| 2 | Reopen on an unsynced Ready to Send order returns it to In Progress for editing | `Should_ReturnToInProgressAndAcceptEdits_When_AnUnsyncedReadyToSendOrderForMurphysPharmacyIsReopened` (store and screen) |
| 3 | Delete order and confirm removes an In Progress order; nothing is sent | `Should_RemoveTheOrderAndSendNothing_When_DeleteOrderIsConfirmedOnAnInProgressOrderWith3Lines` (store and screen), `Should_DeleteAndSendNothing_When_AReadyToSendOrderIsDeletedBeforeSync`, `Should_KeepTheOrder_When_DeleteOrderIsCancelled` |
| 4 | Removing the only line asks "Remove the last line and delete this order?" | `Should_AskRemoveTheLastLineAndDeleteThisOrder_When_TheOnlyLineIsRemoved` (store and screen) |
| 5 | A Sent order shows no edit, reopen or delete controls | `Should_ShowNoEditReopenOrDeleteControls_When_ASentOrderIsOpened`; refused below the UI by `Should_RefuseEveryChange_When_TheOrderIsSent` |
| 6 | An order being uploaded by an in-flight Sync can't be edited until the Sync finishes | `Should_RefuseChangesAndSendTheUploadedBody_When_ASyncIsUploadingTheOrder`, `Should_StayLockedUntilConfirmed_When_TheUploadConfirmationIsLost`, `Should_AllowChangesAgain_When_TheUploadNeverReachedTheServer`, `Should_NotSendTheOrder_When_ItIsReopenedAfterTheSyncReadItsQueue` |

Edges: `Should_RefuseReopenAndEditing_When_TheOrderNeedsAttention` (D2), `Should_KeepTheSavedCall_When_AnOrderIsDeleted` (D3),
`Should_AllowChangesButNotAdditions_When_TheShopIsNoLongerAssigned` (D4/D5), `Should_RefuseTheChangeAndKeep24_When_TheNewQuantityBreaksTheProductRules`,
`Should_RemoveOnlyThatLine_When_OneOfSeveralLinesIsRemoved`, `Should_KeepEveryUnsentAndSentItem_When_AFormat3DatabaseIsUpgraded` (D1),
`Should_OpenTheOrderScreen_When_OpenOrderIsTappedInTheUnsentReview`.

Mutation checks: making `OrderStore` ignore the claim fails scenarios 10 and 11; dropping the "body unchanged" condition
from the claim fails scenario 13. Both were restored.

## Existing tests changed

- `NewOrderComponentTests.Should_ShowReadyToSendAndStopEditing_...`: the Ready view's buttons are now Close, Reopen and
  Delete order (was Close only). It still checks that search and line changes are gone.
- `NeedsAttentionTests.Should_PreserveUnsentAndSentWork_When_AnOlderStoreIsUpgraded`: the fixture that fakes formats 1 and 2
  also drops `upload_claims`, which those formats never had.
- `TabletUploadTests.Should_KeepEveryItem_When_AFormat1UnsentDatabaseIsUpgraded`: the format after upgrade is 4 (was 3).

## Out of scope

Editing Pending orders on the website (E13, R-04); Needs Attention beyond D2 (WI-042); the Sent view with status and
"Open on website" (WI-075); a − stepper on Order Pad rows; the Low tab's REMOVED state (WI-052); price breaks
(WI-065/066); Call linkage (WI-047); Call corrections (WI-055).

## Verification

All on the feature branch before the PR, 3 November 2026:

- Release build of `FieldSales.slnx` (`--no-incremental -m:1`): 0 warnings, 0 errors.
- Complete suite (`dotnet test FieldSales.slnx -c Release --no-build --no-restore -m:1 --settings sqlserver.runsettings`, Docker
  running): **3,062 passed, 0 failed, 0 skipped** across 12 projects. Tablet Core integration 263 (was 243), Tablet Components
  integration 115 (was 108).
- Console status-store tests: 11 passed.
- Android Release build (`scripts/tablet-app.ps1 -Action Build -Configuration Release`): succeeded.
- Not checked on a device: the new controls, confirmations and footer were not seen on the Samsung tablet. Worth a look at
  portrait and landscape, especially the inline change field and the confirmation above the fixed footer.
