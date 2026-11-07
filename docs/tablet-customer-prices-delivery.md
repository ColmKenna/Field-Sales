# WI-064 — Show the customer's resolved price on the Order Pad offline

T-6.4.2 / Pricing US-005 S5, S6; Rep at a Location US-012 S1. Feature branch: `feature/wi-064-show-customer-s-resolved-price`.

After a sync, the tablet holds the tiers each served customer holds and every product's quantity breaks. The Order Pad, quantity
entry and the quantity popover show each product at the shop's price, offline, and a new line captures the engine's winner and
where it came from.

## Decisions

This is the third of the three items the developer asked for with approval assumed ("do the next 3 items, assume approval instead
of asking"). The decisions are the recommended option at each point where a question would have been asked; the plan, scenarios,
Apply, the checkpoint (`Continue T-6.4.2`) and the snapshot version change were taken as approved.

| # | Decision |
|---|---|
| D1 | **Snapshot 1.6** (a minor, additive version; the contract's owner normally reviews it). New families `priceTiers`, `priceTierTerms`, `customerTiers` and `quantityBreaks`, with their counts. Only tiers held by the customers of the rep's assigned Locations, and only terms and breaks for products the snapshot includes. The tablet store moves to format 6. |
| D2 | **One engine on the tablet.** A shop's prices come from one read (`SnapshotStore.ReadLocationPrices`: its customer's tiers, the products' breaks, the price day) and `PriceResolver`. The pad, quantity entry, the popover and capture all use it. |
| D3 | **Pad price.** Each product at the shop's price for its smallest orderable quantity (1 for Each, the minimum for a measure). |
| D4 | **Quantity entry and the popover** show the winner at the quantity typed once the product's rules accept it. The popover names a tier or break winner ("€11.50 · Tier B", "€2.50 · Break: 10+") where it used to say "list price"; the full provenance is WI-065's. |
| D5 | **Capture.** A new line captures the winner at its quantity with every candidate, the winner first and marked. A list-price winner is captured exactly as before ("Base" / "Base price"); tier and break winners carry their own kind and label. |
| D6 | **Later quantity changes keep the captured price**, as decided in WI-059 (D4). Not changed here. WI-066 (break prompts) needs a line to re-price as its quantity crosses a break, so this needs revisiting there. |
| D7 | Lines captured before this change keep their prices; nothing re-prices them. |
| D8 | **Out of scope.** Source and runner-up display and the provenance sheet (T-6.4.3, WI-065), break prompts (T-6.5.1, WI-066), promotions (E18). |
| D9 | **Finish.** Push, PR, merge after CI (merge commit, branch kept), update local main. |

## What was built

- **Contract** (`src/FieldSales.Snapshot.Contracts/SnapshotContract.cs`): version 1.6; `SnapshotPriceTier`, `SnapshotPriceTierTerm`,
  `SnapshotCustomerTier`, `SnapshotQuantityBreak`, `SnapshotTierShapes`; four counts on `SnapshotCounts`.
- **Server** (`TabletSnapshotBuilder.ReadPricingAsync`): the four families, read in a catalogue transaction of their own.
- **Tablet store** (`SnapshotStore.cs`, `SnapshotDocumentReader.cs`): format 6 tables `price_tiers`, `price_tier_terms`,
  `customer_tiers`, `quantity_breaks`; the families are required from 1.6; a term, held tier or break naming something the snapshot
  doesn't carry is refused, as is a percentage term on a price list.
- **Tablet pricing:** `LocationPrices` (resolve, smallest quantity, `Apply` to the index) and `SnapshotStore.ReadLocationPrices`;
  `ProductSearchResult.ListPrice`; `OrderStore.AddLine` captures through `OrderStore.Captured`.
- **Tablet app:** `TabletApp.ReadLocationPrices`; New Order applies the shop's prices to its index and quotes quantity entry with
  them; New Order and Record Call pass them to the `QuantityPopover`.
- **Tests that pinned the old version and format** now expect snapshot 1.6 and format 6, and the format upgrade tests drop the new
  tables too. The API snapshot test helper checks the four new counts.

## Criteria

| Criterion | How it is met |
|---|---|
| 1. The snapshot carries the tiers held by each assigned Location's Customer and each product's quantity breaks | Scenarios 1, 2 (server), 3 (tablet) |
| 2. With no signal, the Order Pad shows each product's resolved price at quantity 1 for this customer, not the base price | Scenario 4 (store and page) |
| 3. The quantity popover and each order line show the engine's winner at the line's quantity; the line captures that price and source | Scenarios 5, 6, 9 |
| 4. Lines captured before this change keep their captured prices | Scenario 7; C3 shows a list-price line is captured as before |

## Agreed scenarios (assumed)

| # | Scenario | Where |
|---|---|---|
| C1 | `Should_ShowTheBasePriceOnThePad_When_TheCustomerHoldsNoTier` | `tests/Integration/FieldSales.Tablet.Core/CustomerPriceCharacterisationTests.cs` |
| C2 | `Should_ShowTheBasePriceInThePopover_When_AddingAProduct` | `tests/Integration/FieldSales.Tablet.Components/NewOrderComponentTests.PriceCharacterisation.cs` |
| C3 | `Should_CaptureTheBasePriceAndItsSource_When_ALineIsAdded` | `CustomerPriceCharacterisationTests.cs` |
| 1 | `Should_CarryTheCustomersTiersAndTheProductsBreaks_When_TheRepSyncs` | `tests/Integration/FieldSales.Api/TabletCustomerPricingTests.cs` |
| 2 | `Should_LeaveOutTiersAndTerms_When_TheRepDoesNotServeTheCustomerOrTheProductIsRestricted` | same |
| 3 | `Should_StoreTiersAndBreaks_When_ASnapshotIsApplied` | `tests/Integration/FieldSales.Tablet.Core/CustomerPricesOnTabletTests.cs` |
| 4 | `Should_ShowTheCustomersPriceOnThePad_When_Offline` | same, and `NewOrderComponentTests.CustomerPrices.cs` |
| 5 | `Should_ShowTheWinnerAtTheTypedQuantity_When_AQuantityReachesABreak` (popover) | `tests/Integration/FieldSales.Tablet.Components/OrderLowTabTests.cs`; quantity entry in `NewOrderComponentTests.CustomerPrices.cs` |
| 6 | `Should_CaptureTheWinnerAndItsSource_When_ALineIsAdded` | `CustomerPricesOnTabletTests.cs` |
| 7 | `Should_KeepCapturedPrices_When_TheCustomersTiersChangeAtTheNextSync` | same |
| 8 | `Should_PriceAtBase_When_TheShopsCustomerHoldsNoTier` | same |
| 9 | `Should_PriceAndCaptureTheTierPriceOfflineAndUploadIt_When_TheRepSyncsAndOrders` | `TabletCustomerPricingTests.cs` |

C1–C3 were written and passed before any production change, and pass after it. Edges from the scenario guidance: a customer whose
tier changed since the last sync, and an order started before a sync and continued after (scenario 7); a shop of a customer with no
tier (scenario 8).

## Mutation checks

Each check was run against the whole test project the scenario lives in.

| Mutation | Tests that failed |
|---|---|
| Every customer's tiers sent, not only the served customers' | 1, 2 |
| Terms sent for products the rep can't see | 1, 2, 9 |
| A term naming an unknown tier accepted by the tablet | 3 |
| A shop prices with every customer's tiers | 8 |
| Breaks not read on the tablet | 3, 6, 8 |
| A list price captured as "List" rather than the base price | C3, 6, 7, and WI-038's capture test |
| Adding more to a line re-prices it | 7 |
| The pad shows the base price, not the shop's | 4 |
| A line captured at its smallest quantity, not the quantity added | 6 |
| The popover quotes the smallest quantity whatever is typed | 5 (popover) |
| Quantity entry ignores the quantity typed | 5 (quantity entry) |
| The popover always says "list price" | 5 (popover) |

The server mutations ran against the 251 API integration tests, the tablet store ones against the 357 tablet core integration tests,
and the page ones against the 221 tablet component tests.

## Verification

- `dotnet build FieldSales.slnx -c Release --no-incremental`: no warnings.
- Full suite (SQL Server): 3,407 passed, 0 failed (WI-063 ended at 3,393; plus C1–C3 and 11 tests for scenarios 1–9).
- `node --test plan_docs/field-sales-delivery/tests/status-store.test.js`: 11 passed.
- Android Release build (`scripts/tablet-app.ps1 -Action Build -Configuration Release`): succeeded, with the tablet's new pricing
  code and storage format.

## Not done here

- **A line keeps its price when its quantity changes** (D6, WI-059's rule). Break prompts (WI-066) will need this decided again.
- **Where a price came from in full** (runner-up, every candidate on demand) is WI-065's; the popover names only the winner's source.
- **Snapshot size:** four small families, bounded by the tiers the rep's customers hold and the included products' breaks.
