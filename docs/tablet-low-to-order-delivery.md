# WI-051 — Mark a product Low and add it to the order through a quantity popover

T-5.5.1 / REP-US-008; UX T-06, T-07, T7.6–T7.8, T7.13. Feature branch: `feature/wi-051-mark-product-low-add-it`.

On a Call's stock check the rep ticks **Low** on a product and taps **Add to order**. A popover asks for the quantity, and the
product goes on the shop's In Progress order, which is found or started for the Call. The row then reads **On order (24)**.

## Scope and approved decisions

The developer replied **Approved** to the plan, decisions D1–D10 and the 15-scenario list, then **Apply**. After Apply, two
questions were asked once and answered (D1 revised, order-only Call). At the checkpoint (the link with find-or-create, and the
popover) the developer replied `Continue T-5.5.1`.

| # | Decision |
|---|---|
| D1 (revised) | The Call–Order link is the Order's existing reserved `callId`. A new order started from a Call gets it, and an In Progress order with no Call takes it on the first add from a Call. An order already linked to another Call keeps that link. No Call contract, server or schema change. (Originally approved: a new `orderId` on the Call with a server column. Dropped once the existing `callId` came to light.) |
| D2 | Find-or-create picks the Call's linked In Progress order, else the rep's most recently saved In Progress order at the shop, else starts one. Picking and adding happen in one step under the per-rep order lock. An order being sent, refused or Ready to Send is never picked, so if the linked order leaves In Progress the next add finds or starts another. |
| D3 | In person, every row has a Low tick, replacing WI-050's read-only note. Ticking it shows Add to order. Unticking keeps the count, because 0 counted is a real count In person. Phone keeps WI-050 D6. |
| D4 | On Phone, Add to order works on every row and doesn't mark Low. A row that is only on the order isn't saved as a stock entry. |
| D5 | **On order (N)** replaces Add to order wherever it would show, once the product is on the Call's order. That includes lines added from order entry. Measured products show the unit ("On order (2.5 kg)"). Unticking Low never takes a line off. |
| D6 | The popover shows the name, "counted 3" or "not counted", "€8.95 · list price" ("per kg" for measures) and an empty quantity that is never pre-filled. The product's own rule message shows once something is typed, and Add to order stays unavailable until the quantity is valid. With no price: "No price yet — this product can't be ordered." Store refusals show inside the popover. Cancel and ✕ close it, and so does a successful add. Lines keep the contract's "Base price" label. |
| D7 | `Components/Shared/QuantityPopover.razor` is reusable by the order's Low tab (T-5.1.4), with a slot under the price for T-6.5.1's break prompt. |
| D8 | Leaving an unsaved Call keeps the lines added to the order. The leave dialog adds "Lines added to the order stay on it." |
| D9 (revised) | Server: nothing new. It already stores `ReceivedOrders.CallId`. |
| — | A Call whose only record is lines added to the order isn't saved; the S5 rule stands (developer, after Apply). |
| D10 | Finish: push, PR, merge after CI (merge commit, branch kept), update local main. |

Out of scope: the Low tab (T-5.1.4), break prompts (T-6.5.1), replacements (E10), "Outside your ranges" (S5, E9), S9, S13.

## Tablet

- `OrderStore.FindForCall(callId, locationId)` and `OrderStore.AddFromCall(callId, locationId, productId, quantity)` follow D2.
  `AddLine` and `AddFromCall` share one core, which runs under the gate.
- `TabletApp.ReadCallOrder` and `TabletApp.AddOrderLineFromCall`.
- `StockLine.Tick()` is the In-person Low tick. `CallDraft.AddedToOrder` drives the leave dialog's line.
- `RecordCall.razor` reads the Call's order when it opens and replaces it with each add's result. It shows the Low tick, Add to
  order, On order (N) and the popover.

## Acceptance criteria and agreed scenarios

| # | Scenario | Evidence |
|---|---|---|
| 1 | S7: popover with name, counted 3, list price, empty quantity | `Should_OpenThePopoverWithNameCount3ListPriceAndAnEmptyQuantity_When_ColdFluRelief16sIsCounted3AndMarkedLow` |
| 2 | S1: 24 added, an In Progress order started for Murphy's and linked to the Call | component `Should_CreateAnInProgressOrderForMurphysWithThatLineLinkedToThisCall_When_24IsAdded`; Tablet.Core `OrderFromCallTests.Should_CreateAnInProgressOrderForMurphysLinkedToTheCall_When_24IsAddedFromACall` |
| 3 | S2: a 3rd line, no second order | `OrderFromCallTests.Should_AddA3rdLineAndCreateNoSecondOrder_When_MurphysAlreadyHasAnInProgressOrderWith2Lines` |
| 4 | S3: On order (24), including an order-entry line (2.5 kg) | `Should_ShowOnOrder24InsteadOfAddToOrder_When_ColdFluRelief16sIsOnTheOrderWith24` |
| 5 | S4: unticking Low leaves the line at 24; leave dialog says lines stay | `Should_LeaveTheOrderLineAt24_When_TheLowMarkIsRemoved` |
| 6 | S6: 0 → "Enter a quantity of 1 or more", Add unavailable | `Should_ShowEnterAQuantityOf1OrMoreAndKeepAddUnavailable_When_0IsEnteredForAnEachProduct` |
| 7 | S12: kg below minimum / off the step | `Should_ShowTheMeasureMessageAndKeepAddUnavailable_When_AKgQuantityIsBelowTheMinimumOrOffTheStep` |
| 8 | S11: Cancel after typing adds nothing | `Should_AddNothingAndKeepTheItemNotAdded_When_CancelIsTappedAfterTyping` |
| 9 | S8: empty quantity after ✕ and from another row | `Should_OpenWithAnEmptyQuantity_When_ThePopoverIsReopenedOrOpenedFromAnotherRow` |
| 10 | One order for the Call (eight adds released together) | `OrderFromCallTests.Should_UseOneOrderForTheCall_When_TwoLowProductsAreAdded` |
| 11 | Linked order marked Ready: another is started and linked | `OrderFromCallTests.Should_FindOrCreateAgainAndMoveTheLink_When_TheLinkedOrderWasMarkedReadyToSendElsewhere` |
| 12 | Phone Add to order without Low; order-only Call not saved | `Should_AddToTheOrderWithoutMarkingLow_When_AddToOrderIsUsedOnAPhoneRow` |
| 13 | In person, untick keeps a count of 0 | `Should_KeepTheCount_When_LowIsUntickedInPersonOnARowCounted0` |
| 14 | No price: message, Add unavailable | `Should_ShowNoPriceYetAndKeepAddUnavailable_When_TheLowProductHasNoPrice` |
| 15 | The order's `callId` reaches `ReceivedOrders.CallId` (tablet, upload, SQL Server) | `SuggestedListTests.Should_KeepTheCallLink_When_AnOrderAddedFromACallIsUploaded` |

One extra storage check, `Should_PreferTheCallsOwnOrderAndKeepAnotherCallsLink_When_TheShopHasTwoInProgressOrders`, pins D2's
preference order.

Mutation checks:

| Mutation | Tests that failed |
|---|---|
| The order lock removed from `AddFromCall` | scenario 10, 3 runs of 3. An earlier two-add version didn't overlap reliably and was replaced. |
| On order never shown | scenarios 4, 5, 7, 12 |
| Quantity pre-filled with 1 | scenarios 1, 9 |
| In-person untick clearing Out's count | scenario 13 |

## Existing tests changed on purpose

- WI-050 scenario 1, `Should_ShowStockMentionedWithTheSame16ProductsAndAFilterBox_When_PhoneIsChosenAtDoyles`, now expects Add to
  order to be available on every phone row (D4).
- WI-050 scenario 11, `Should_SaveLowMarksAndShowThem_When_TheCallIsSavedInPersonAfterPhone`, now checks the In-person Low tick
  instead of the read-only "Low"/"Out" note (D3).
- The tablet screen test harness's element lookups now search child components too, in document order, so the shared popover can
  be driven.

## Not done here

The order's Low tab with the same Call's rows (T-5.1.4), break prompts (T-6.5.1), replacements (E10), "Outside your ranges" (E9).
Not checked on a device.
