# WI-062 — Set quantity breaks on a product

T-6.3.1 / Pricing US-004 S1–S4; uxdocs 02 H-22 (H22.1, H22.2). Feature branch: `feature/wi-062-set-quantity-breaks-product`.

A Head Office User can now give a product prices that apply from a quantity upward ("from 10: €2.00", "from 10 kg: €4.00"), on the
product record. The breaks reach the price engine, which already knew how to use them. A break that can never apply is saved, with a
sentence saying why.

## Decisions

After WI-061 the developer asked for the next three items with approval assumed instead of asked. So these decisions are the
recommended option at each point where a question would have been asked, and the plan, scenarios, Apply and the checkpoint
(`Continue T-6.3.1`) were taken as approved.

| # | Decision |
|---|---|
| D1 | **Storage.** A new table `ProductQuantityBreaks` (product, from quantity, price), keyed by product and quantity. Breaks belong to the product, not a tier, and aren't dated: they apply from when they are saved. |
| D2 | **Editing (H22.1).** A product's breaks are saved as a set: `PUT /catalogue/products/{id}/quantity-breaks` replaces them. On the product record, a "Quantity breaks" section after Base price shows the base price ("Base price €2.20 each"), then the breaks by quantity, each editable with Remove, a blank row to add one, and Save. |
| D3 | **Rules.** A threshold is an orderable quantity of the product above the smallest one: whole for Each and more than 1; a multiple of the step and more than the minimum for measures ("Enter multiples of 0.5 kg"). Each threshold once. Prices more than €0.00, to the cent. |
| D4 | **Warnings (H22.2).** Saved anyway, worked out on reading: "This break is higher than the one below it and will never apply." (a cheaper break sits at a smaller quantity) and "This break is above the base price and will never apply." (above today's base price). Equal prices aren't flagged. |
| D5 | **Unit changes.** A unit, step or minimum change that would leave a break off the new rules is refused: "Change or remove the quantity breaks first: from 12.5 kg doesn't fit." |
| D6 | **Engine.** `ToPricedProduct()` passes the breaks. |
| D7 | **Access.** The existing `HeadOfficeCatalogue` policy. |
| D8 | **Out of scope.** Breaks in the snapshot and on the tablet (T-6.4.2, WI-064), break prompts (T-6.5.1), breaks per tier. |
| D9 | **Finish.** Push, PR, merge after CI (merge commit, branch kept), update local main. |

## What was built

- **Model** (`src/FieldSales.Api/Catalogue/ProductQuantityBreak.cs`, `Product.cs`):
  - `ProductQuantityBreak` with `IsAboveCheaperBreak` and `IsAboveBase`; `QuantityBreakRules` (threshold and price rules, messages).
  - `Product.QuantityBreaks`, `SetQuantityBreaks` (a break at a quantity it had is repriced, one no longer listed goes, a new one is
    added), `BreaksMisfit`, and the guard in `SetQuantityRules`.
- **Storage:** configuration in `CatalogueDbContext` and the migration `20261106203208_AddProductQuantityBreaks`
  (`CK_ProductQuantityBreaks_Positive`, foreign key to the product).
- **Engine input:** `EngineInputs.ToPricedProduct()` passes each product's breaks.
- **Contracts:** `ProductQuantityBreakItem` (with the two warning flags) on `ProductDetails.QuantityBreaks`;
  `SetProductQuantityBreaksRequest`, `QuantityBreakRequestItem`.
- **API** (`ProductEndpoints.cs`): details list the breaks by quantity with their flags; `PUT …/quantity-breaks`; the unit endpoint
  refuses a change that leaves a break off its rules.
- **Website:** `CatalogueApiClient.QuantityBreaks.cs`; the "Quantity breaks" section on `/HeadOffice/Products/{id}`. Blank rows are
  left out, Remove drops its row, and a refusal is shown on the row it came from.
- **Test support:** the web test fixture `ProductApplication` clears breaks before products.

## Criteria

| Criterion | How it is met |
|---|---|
| 1. "Throat Lozenges 36s" (Each, €2.20) with breaks from 10 at €2.00 and from 30 at €1.75 resolves 12 at €2.00 and 30 at €1.75 through the engine | Scenario 1 |
| 2. "Loose Herbal Tea" (kg, €4.80, step 0.5) with from 10 kg at €4.00 and from 20 kg at €3.60 resolves 12 kg at €4.00 per kg | Scenario 2 |
| 3. From 30 at €2.30 above from 10 at €2.00 warns "This break is higher than the one below it and will never apply" and can still be saved | Scenario 3 (API), 5 (page), 13 (end to end) |
| 4. Breaks are sorted by quantity, with the base price shown above them | Scenario 5, and 13 |
| 5. Measure break thresholds are multiples of the product's step | Scenario 6; kept true through unit changes by scenario 10 |
| Story S4: a tier price below the break wins | Scenario 4 |

## Agreed scenarios (assumed)

| # | Scenario | Where |
|---|---|---|
| 1 | `Should_PriceTwelveAt200AndThirtyAt175_When_ThroatLozengesHasBreaksFrom10And30` | `tests/Integration/FieldSales.Api/QuantityBreakPricingTests.cs` |
| 2 | `Should_PriceTwelveKgAt400PerKg_When_LooseHerbalTeaHasBreaksFrom10KgAnd20Kg` | same |
| 3 | `Should_SaveAndWarn_When_ABreakFrom30At230IsAboveTheFrom10At200` | `tests/Integration/FieldSales.Api/QuantityBreakApiTests.cs` |
| 4 | `Should_LetTheTierPriceWin_When_ATierPricesTheProductBelowTheBreak` | `QuantityBreakPricingTests.cs` |
| 5 | `Should_ListBreaksByQuantityBelowTheBasePrice_When_HeadOfficeOpensTheProduct` | `tests/Integration/FieldSales.Web/Catalogue/QuantityBreakPageTests.cs` |
| 6 | `Should_RefuseAThreshold_When_ItIsNotAQuantityAboveTheSmallestOrder` | `QuantityBreakApiTests.cs` |
| 7 | `Should_RefuseTheBreaks_When_TwoHaveTheSameQuantity` | same |
| 8 | `Should_RefuseAPrice_When_ItIsOutsideTheLimits` | same |
| 9 | `Should_SaveAndWarn_When_ABreakIsAboveTheBasePrice` | same |
| 10 | `Should_RefuseAUnitChange_When_ABreakWouldNoLongerFit` | same |
| 11 | `Should_RefuseQuantityBreaks_When_TheCallerIsNotAHeadOfficeUser` | same |
| 12 | `Should_EditBreaksInPlace_When_HeadOfficeUsesTheSection` | `QuantityBreakPageTests.cs` |
| 13 | `Should_SaveFlagAndCorrectBreaks_When_HeadOfficeUsesTheProductRecord` | `tests/Integration/FieldSales.Web/Catalogue/QuantityBreakIntegrationTests.cs` |

Edges from the scenario guidance: a break at quantity 1 is refused (scenario 6); a break above base price is warned (9); two breaks
at the same quantity are refused (7); a measure break off the step is refused (6).

## Mutation checks

Each check was run against the whole test project the scenario lives in.

| Mutation | Tests that failed |
|---|---|
| Breaks not passed to the engine | 1, 2, 3, 4 |
| The cheaper-break check also looks at larger quantities | 9 |
| A break equal to the base price flagged | 9 |
| A break may start at the smallest orderable quantity | 6 |
| Two breaks at the same quantity allowed | 7 |
| A unit change ignores the breaks | 10 |
| A break left out of the set stays | 3 |
| Every saved break warned | 5, 13 |
| A refusal shown on the position sent, not the form row | 12 |
| Blank rows sent to the API | 12 |

The API mutations ran against the 240 API integration tests, the website ones against the 884 web integration tests.

## Verification

- `dotnet build FieldSales.slnx -c Release --no-incremental`: no warnings.
- `dotnet ef migrations has-pending-model-changes` (catalogue): no pending changes.
- Full suite (SQL Server): 3,382 passed, 0 failed (WI-061 ended at 3,369; plus the 13 scenarios).
- `node --test plan_docs/field-sales-delivery/tests/status-store.test.js`: 11 passed.
- Android Release build (`scripts/tablet-app.ps1 -Action Build -Configuration Release`): succeeded (the tablet is unchanged).

## Not done here

- **The tablet doesn't have breaks yet.** The snapshot carries them with T-6.4.2 (WI-064).
- **No break prompts.** "2 more for €2.00 each" comes with T-6.5.1.
- **A 30 break at €2.30 on a €2.20 product carries both warnings,** because it is above the cheaper break and above the base price.
