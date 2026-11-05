# WI-053 — Carry the last call's gaps to an order with no call

T-5.1.5 / REP-US-012 S19–S22; UX T7.13; US-NEW-002 AC-8–12. Feature branch: `feature/wi-053-carry-last-call-s-gaps`.

When an order has no Call, its **Low** tab now shows the Low items from the shop's most recent Call that nobody has ordered
since. The tab reads "No stock check with this order." and then "Still open from your call at Carey's - Tue 15 Sep". Each row
reads "marked Low 15 Sep", and the tab label counts these items. **Record call** from the tab links the order to the new Call
when it is saved.

## Scope and approved decisions

The developer replied **Approved** to the plan, decisions D1–D7 and the 12-scenario list, then **Apply**. At the checkpoint
the developer, as the snapshot contract's owner, answered "ordered since is any order since the date", then replied
`Continue T-5.1.5`.

| # | Decision |
|---|---|
| D1 | Snapshot **1.4** (a minor version, so older apps ignore it) adds one family, `lastCallLowLines`. Each row names a shop's most recent Call (any channel, recorded by anyone) and one of its Low products that the rep can see. Products on any order at that shop dated on or after the Call's date are left out. The tablet's local format goes from 3 to 4, with an upgrade step. |
| D2 | **"Ordered since" means any order at the shop dated on or after the Call's date, whatever its status** (the owner's ruling at the checkpoint). On the server that is every received order, with no status filter. On the tablet it is also every one of its own orders at the shop, unsent in any state or already sent. The exception is the order on screen: its lines show those items ADDED. |
| D3 | The most recent Call is the snapshot's one, unless this tablet holds a Call at the shop saved after the snapshot was built. That Call can't be in the snapshot, so it is the newer one, and its own Low items carry after the same subtractions. |
| D4 | Wording: always "No stock check with this order.", then "Still open from your call at {shop} - {ddd d MMM}". Rows read "marked Low {d MMM}". The tab count includes carried items that are not yet added. There is no age limit, and a **Record call** link appears in both cases. "your call" is used even when another rep recorded it. |
| D5 | Record call from the Low tab opens `/locations/{id}/orders/{orderId}/calls/new`, where the back link reads **Back to order**. On Save, `OrderStore.LinkToCall` links the order to the Call (only an In Progress order with no Call), and the rep returns to order entry on that Call's route, where the Low tab lists the Call's own Low items. If the order had no lines yet, it doesn't exist, so the rep gets a new order opened from the Call. If the order can no longer be linked, the rep returns to the order as it was. |
| D6 | Out of scope: CAN'T ADD (E8), so S19's Aftersun row can't appear yet; REPLACED (E10); customer online and chain orders until they exist. |
| D7 | Finish: push, PR, merge after CI (merge commit, branch kept), update local main. |

## Snapshot 1.4 (reviewed by the owner at the checkpoint)

```json
"lastCallLowLines": [ { "locationId", "callId", "calledOn", "productId" } ],
"manifest.counts": { …, "lastCallLowLines" }
```

- A 1.3 app ignores the new family. A 1.4 app reads a 1.3 snapshot as having no gaps.
- **Server:** `TabletSnapshotBuilder.ReadLastCallLowLinesAsync`.
  1. It takes each shop's most recent received Call, by the date it was made, then save time, then id.
  2. It takes that Call's Low stock entries for products the rep can see.
  3. It removes every product on a received order at the shop with `TakenOn >= CalledOn`, in any status, reading lines from the received body.
- **Tablet:** a `last_call_low_lines` table, local format 4. The upgrade chain 1 → 2 → 3 → 4 keeps every snapshot and all Unsent work. Import refuses:
  - a row naming a product or shop outside the snapshot;
  - a repeated row;
  - two Calls for one shop;
  - a missing family or a count mismatch;
  - gaps in a snapshot older than 1.4.
- **Size:** at the target scale (500 shops, 5–30 carried products each), about 2,500–15,000 rows of roughly 130 bytes. That is 0.3–2 MB uncompressed, against the 120,000 history lines the target profile already carries within the 90-second refresh.
- **Routes covered:** tablet uploads (`TabletWorkReceiver`) are the only way an order enters the system today. Customer online and chain orders will join the same query when they exist. Orders still unsynced on another rep's tablet can't be known.

## Characterisation (checkpoint), passing before and after

- C1 `Should_ListOnlyItsOwnCallsLowItems_When_TheOrderIsLinkedToACallAndAnotherCallAtTheShopLeftGaps`
- Also unchanged: WI-052's Low tab tests, the order-entry characterisation, `SnapshotStockHistoryTests` and `SuggestedListTests`.

## Agreed scenarios

| # | Scenario | Evidence |
|---|---|---|
| 1 | S19: "Still open from your call at Carey's - Tue 15 Sep", Nappy Wipes only (Sudocrem ordered 18 Sep) | `OrderCarriedGapsTests.Should_ReadStillOpenFromYourCallAtCareysTue15SepAndListNappyWipesOnly_When_SudocremWasOrderedThereOn18Sep` |
| 2 | S20: 10 weeks ago, labelled with that date | `Should_CarryTheGapsLabelledWithThatCallsDate_When_TheMostRecentCallWas10WeeksAgo` |
| 3 | S21: no earlier Call: Low (0), message, Record call | `Should_ShowLow0NoStockCheckWithThisOrderAndRecordCall_When_TheShopHasNoEarlierCall` |
| 4 | S21: every gap ordered since | `Should_ShowLow0NoStockCheckWithThisOrderAndRecordCall_When_EveryGapWasOrderedSince` |
| 5 | S22: Record call, Save links the order, then that Call's Low items | `Should_LinkTheOrderAndShowThatCallsLowItems_When_ACallIsRecordedAndSavedFromTheLowTab` |
| 6 | Back returns to the order (or a new order when it had no lines) | `Should_ReturnToTheOrder_When_BackIsTappedOnRecordCallOpenedFromTheLowTab` |
| 7 | T7.13: a carried item ADDED through the popover | `Should_ShowADDED_When_ACarriedItemIsAddedThroughThePopover` |
| 8 | A gap dropped when on another order on this tablet since the Call, in any state | `CarriedGapsTests.Should_DropAGap_When_ItIsOnAnotherOrderOnThisTabletSinceTheCall` |
| 9 | A newer unsynced Call's gaps carry instead | `CarriedGapsTests.Should_CarryTheNewerCallsGaps_When_TheTabletHoldsAnUnsyncedCallAtTheShop` |
| 10 | Server: the most recent Call's Low items minus products ordered since | `SuggestedListTests.Should_SendTheMostRecentCallsLowItemsMinusProductsOrderedSince_When_TheSnapshotIsBuilt` |
| 11 | Tablet import of 1.4; 1.3 still imports; refusals; upgrade to format 4 | `SnapshotLastCallGapsTests` (4 tests, 10 cases) |
| 12 | End to end: a Call's gap reaches a later order with no Call | `SuggestedListTests.Should_CarryAGapToALaterOrderWithNoCall_When_ACallWithLowItemsIsSyncedAndTheSnapshotRebuilt` |

Mutation checks:

| Mutation | Tests that failed |
|---|---|
| Server: orders on the Call's date not counted (`>` for `>=`) | scenario 10 |
| Tablet: the order on screen counted as ordered since | scenario 8 |
| Tablet: a newer unsynced Call ignored | scenario 9 |
| Tablet: orders on the Call's date not counted | scenarios 4, 9 |

## Verification

- `dotnet build FieldSales.slnx -c Release --no-incremental`: no warnings.
- Full suite (SQL Server): 3,215 passed, 0 failed, including Tablet.Components 186, Tablet.Core 317 and Api 200.
- `node --test plan_docs/field-sales-delivery/tests/status-store.test.js`: 11 passed.
- Android Release build (`scripts/tablet-app.ps1 -Action Build -Configuration Release`): succeeded.

## Existing tests changed

- Snapshot version pins 1.3 → 1.4: two API assertions and two Tablet.Core contract assertions.
- Local format pins 3 → 4: the attribute, stock history and contact status upgrade tests.
- The "newer app" format test now uses 5.
- The API test reader checks the new count, and the Tablet.Core snapshot builder adds an empty family for 1.4.

## Not done here

CAN'T ADD (E8), REPLACED (E10), customer online and chain orders. Not checked on a device.
