# WI-052 — Catch up on Low items in the Low tab

T-5.1.4 / REP-US-012 S3, S7–S9, S12, S13; REP-US-008 S13; UX T-07 (T7.1, T7.6, T7.8). Feature branch:
`feature/wi-052-catch-up-low-items-low`.

Order entry gets a **Low** tab beside the Order pad. It lists each product marked Low on the Call as *not added*, *ADDED (qty)*
or *REMOVED*. The tab label counts what still needs doing, and **Add** opens the same quantity popover as the stock check.

## Scope and approved decisions

The developer replied **Approved** to the plan, decisions D1–D9 and the 14-scenario list, then **Apply**. At the checkpoint
(characterisation tests passing, and the state design) the developer replied "proceed", which was taken as the resume trigger
`Continue T-5.1.4`.

| # | Decision |
|---|---|
| D1 | Two tabs: **Order pad** (today's content exactly, search box included) and **Low (n)**. "Search all" waits for Ranges to split the pad from the catalogue. The tab strip shows only while lines can be added (a new or In Progress order). |
| D2 | Record call gets **Open order**. It opens the Call's order (the one WI-051's find-or-create picks) or a new one, on the `/locations/{id}/calls/{callId}/orders/...` routes. The back link reads **Back to call**, and Save and close and deleting the order go back to the Call. The draft is kept before leaving. |
| D3 | Order entry always adds to the order on screen. Opened from a Call, it uses `OrderStore.AddLine(..., callId)` (`TabletApp.AddOrderLineForCall`), so a new order, or one with no Call yet, is linked to the Call on its first line, from the pad or the Low tab. Standalone orders are unchanged. |
| D4 | The Low items come from the Call order entry was opened from, else the order's `callId`. They are read from the Call's draft while it is being recorded, else from the saved Call (`CallStore.Read`, unsent or sent). With no Call the tab reads `Low (0)` and "No stock check with this order.". |
| D5 | `LowItems.Of(entries, order, takenOff)` gives every Low entry one state. **ADDED** whenever the order has a line for the product, however it got there. **REMOVED** when not on the order and the Call's draft recorded that its line was taken off (`CallDraft.TakenOff`, written by order entry after a successful line removal or order deletion). **not added** otherwise. The order alone decides ADDED, so the tab can't disagree with it. Limit: a saved Call keeps no "taken off", so a removed item then reads not added. |
| D6 | Each row shows the name and the state (`not added`, `ADDED - 24`, `REMOVED`), then "counted 3", "not counted" or "you took this off the order". **Add** on not added and REMOVED rows opens `QuantityPopover`, which closes back to the tab. An ADDED row's quantity is changed on its order line. There is no Add all. |
| D7 | `Low (n)` counts not added only. Unticking Low drops the item from the tab and never touches the order. |
| D8 | Out of scope: the last Call's gaps on an order with no Call (T-5.1.5 / WI-053), REPLACED (E10), CAN'T ADD (E8), a stepper on ADDED rows, Search all. No server or contract change. |
| D9 | Finish: push, PR, merge after CI (merge commit, branch kept), update local main. |

New wording not in the specs: "Nothing marked Low on this call." for a Call with no Low marks, and "From your stock check at
{shop}" above the rows (taken from the T-07 wireframe).

## Characterisation (checkpoint), passing before and after

- C1 `Should_ShowTheSearchBoxThePadAndTheOrdersLines_When_AnInProgressOrderIsOpenedById`
- C2 `Should_AddEveryLineToTheOrderOnScreenWithoutACall_When_ANewOrderIsBuiltFromTheShop`
- C3b `Should_RemoveTheLineWithoutAsking_When_OneOfTwoLinesIsRemoved`. C3 is the existing
  `Should_AskRemoveTheLastLineAndDeleteThisOrder_When_TheOnlyLineIsRemoved`.
- Also unchanged: the other order-entry component tests, WI-051's popover tests and the find-or-create tests.

## Acceptance criteria and agreed scenarios

| # | Scenario | Evidence |
|---|---|---|
| 1 | S3: from a Call with 2 Low, 1 added: 1 line, Low tab one ADDED and one not added, Back to call | `OrderLowTabTests.Should_ShowTheInProgressOrderWith1LineAndTheLowTabWithOneAddedOneNot_When_OrderEntryIsOpenedFromACallWith2LowLines1Added` |
| 2 | S7: exactly one state each | `Should_ListEachLowItemWithExactlyOneState_When_TheCallHasItemsNotAddedAddedAndRemoved` |
| 3 | S8: count not added only | `Should_CountOnlyNotAddedItemsInTheTabLabel_When_ItemsAreAddedRemovedAndNotAdded` |
| 4 | S9: removed line → REMOVED with Add | `Should_ShowREMOVEDWithAnAddAction_When_AnAddedLowItemsLineIsRemovedFromTheOrder` |
| 5 | US-008 S13, S10, S14: same popover, ADDED with quantity, back to the tab | `Should_OpenTheSamePopoverAndShowADDEDWithItsQuantity_When_AddIsTappedOnANotAddedItem` |
| 6 | REMOVED re-added | `Should_ShowADDEDAgain_When_AREMOVEDItemIsAddedThroughThePopover` |
| 7 | S12: no Add all | `Should_OfferNoWayToAddThemAllAtOnce_When_SeveralItemsAreNotAdded` |
| 8 | S13: untick leaves the line, drops the item | `Should_LeaveTheOrderLineAndDropTheItem_When_LowIsUntickedOnTheStockCheck` |
| 9 | Changed quantity shows | `Should_ShowTheNewQuantity_When_AnAddedItemsLineIsChangedOnTheOrder` |
| 10 | Added from the pad still ADDED; the new order is the Call's | `Should_ShowADDED_When_ALowProductIsAddedFromTheOrderPadInstead` |
| 11 | No Call: Low (0), "No stock check with this order." | `Should_ShowLow0AndNoStockCheckWithThisOrder_When_TheOrderHasNoCall` |
| 12 | Saved Call's Low items; removed then reads not added | `Should_ListTheSavedCallsLowItems_When_TheOrderIsOpenedAfterTheCallWasSaved` |
| 13 | A new order from order entry opened from a Call is linked to it | `OrderFromCallTests.Should_LinkANewOrderToTheCall_When_ItsFirstLineIsAddedFromOrderEntryOpenedFromTheCall` |
| 14 | Open order, then back to the same draft; Save and close returns to the Call | `Should_OpenTheCallsOrderAndComeBackToTheDraft_When_OpenOrderThenBackToCallAreTapped` |

Mutation checks:

| Mutation | Tests that failed |
|---|---|
| "Taken off" ignored | scenarios 2, 3, 4, 6 |
| The count including REMOVED | scenarios 3, 4 |
| A removal not recorded | scenario 4 |
| Order entry opened from a Call using the plain add | scenarios 5, 10 |

## Test harness changes

- Each Low row carries `data-low-item="{name}: {state}"`, which the tests read.
- The general screen harness gains `ClickContaining`, to pick a search result by product name.

## Not done here

The last Call's gaps on an order with no Call, with its Record call link (T-5.1.5), REPLACED (E10), CAN'T ADD (E8), Search all.
Not checked on a device.
