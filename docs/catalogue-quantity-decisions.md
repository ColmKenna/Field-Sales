# Product quantities — WI-007

Date: 2026-09-30. Source: T-1.4.1 / PRD-US-003 and PRD-US-002 S5. Status: complete, all acceptance and regression checks passed.

## Approved plan and policies

The developer approved the plan and scenario list before implementation, including these refinements:

- Use exact decimals with precision 18 and scale 6. The maximum is `999999999999.999999`; values outside the range or requiring more fractional precision are rejected. There is no rounding or floating-point path.
- Stock counts may be zero. Order quantities remain positive and must satisfy the product's minimum and step. Free-of-charge items use a positive quantity and a zero price; that feature belongs to WI-156, as confirmed by `plan_docs/stories/pricing.md`.
- Support Each, kg, litre and metre. Each orders are whole numbers of one or more. Measures require a positive step; a blank minimum defaults to that step, and a supplied minimum must be a positive multiple of it.
- When a product's unit changes, retain its numeric base-price amount. Before saving, show the resulting price and flag its changed unit basis, for example `€4.80 per Each → €4.80 per kg`.

Existing product persistence and website behaviour was characterised first: 11 API product/domain persistence cases and 19 product website/API/SQL cases passed.

## Quantity contract for review

`FieldSales.Quantities` is a dependency-free .NET 10 library. API, website and unit tests reference it. It has no EF Core, ASP.NET, tablet-runtime, authentication or price dependencies.

`Quantity` is the single immutable amount-and-unit value type. Its unit is part of its value, so `12 Each` differs from `12 kg`; a later product-unit change cannot reinterpret an already recorded quantity. Its factory accepts zero for stock counts, whole amounts for Each, and exact supported decimal amounts for measures. A default value is valid zero Each.

`QuantityRules` is the immutable product order-entry rule. It owns the selected unit, step and minimum, validates configuration, and returns a `Quantity` or a validation error. Invalid values are never returned as successful quantities. Hidden measure inputs cannot alter Each's fixed step/minimum of one.

Public entry points:

```csharp
// Raw recorded quantity: stock counts may be zero.
Quantity zeroCount = Quantity.Create(UnitOfMeasure.Kilogram, 0m);
bool representable = Quantity.TryCreate(UnitOfMeasure.Kilogram, 1.5m, out Quantity quantity);

// Product order rule: defaults, field errors and exact validation live here.
bool validRules = QuantityRules.TryCreate("kg", 0.5m, 1m,
    out QuantityRules? rules, out IReadOnlyDictionary<string, string[]> errors);
if (validRules)
{
    QuantityValidationResult result = rules.ValidateOrder(1.5m);
    // result.IsValid == true; result.Quantity is 1.5 kg; result.Error is null.
}
```

The factory returns field errors keyed by `Unit`, `QuantityStep` and `MinimumQuantity`, ready for the later API/BFF form mapping. `Minimum 0.7 / Step 0.5` reports exactly `Minimum must be a multiple of the step`. Order validation reports `Enter a quantity of 1 or more` for Each and `Enter at least 1.0 kg in steps of 0.5 kg` for kg with that rule, independently of server culture.

The contract preserves numeric values rather than normalising by rounding. Representability uses decimal remainder against `0.000001`; step validation uses decimal remainder against the configured step. Factories prevent unsupported units, invalid amounts and invalid rule configurations from entering the value objects. Consumers should construct quantities through these factories rather than bypassing them in persistence or transport adapters.

## Checkpoint evidence

`FieldSales.Api.Tests/QuantityTests.cs`: 40 cases passed, none skipped. Scenarios cover:

- 1.0, 1.5 and 2.0 kg with step 0.5 and minimum 1.0.
- Below-minimum and between-step amounts, including one smallest increment either side of a valid amount.
- Blank minimum, invalid minimum and exact source wording.
- Whole positive Each orders, including submitted hidden measure fields.
- Quarter and tenth steps, litre/metre units and exact `0.1m + 0.2m` arithmetic.
- Maximum and excess range/precision, negative amounts and unsupported units.
- Zero stock counts versus rejected zero orders using the same quantity type.
- Retained recorded units and culture-independent error wording.

The complete solution builds with zero warnings and errors. The contract source contains no float/double arithmetic or rounding calls.

The developer reviewed the concrete contract checkpoint in commit `f4f03bb` and explicitly resumed with `Continue T-1.4.1`. The subsequent work preserves that reviewed quantity type and validation API.

## Persistence and protected API

`Product.SetQuantityRules` applies the reviewed rule and stores a unit code plus nullable `QuantityStep` and `MinimumQuantity`. Each has null measure fields and derives its whole-number, minimum-one rule. Measures store the validated effective minimum. `Product.GetQuantityRules` reconstructs the same shared validator; it does not reimplement step arithmetic.

Migration `20260930202155_AddProductQuantityRules` adds only the two nullable `decimal(18,6)` columns and a check constraint. Existing Each products remain valid with null fields. The constraint requires positive, nonnull fields for kg/litre/metre and a minimum divisible by the step; `NULLIF` avoids division by zero. Existing product identity, category, attributes and base-price entries are untouched.

The existing protected create endpoint accepts the supported unit and optional measure fields. `PUT /catalogue/products/{id}/unit` validates through the shared factory and updates only the unit rule. Detail and category-product projections include the saved step/minimum. The existing Head Office policy and BFF bearer-token path remain the access boundary.

## Product forms and price preview

Create and Detail share `_UnitFields.cshtml`, with Each/kg/litre/metre selection, measured fields and a resulting-price preview. Each hides and disables the measure fields. Server-side Each handling also discards submitted measure values, including stale invalid inputs, so they cannot change its fixed rule. The predecessor's website form omitted a posted Unit because it was fixed to Each; the create handler preserves those submissions as Each.

`product-unit-form.js` immediately updates visibility, required-step state, price entry wording and the price-basis flag on unit selection. It formats entered price text without binary conversion or rounding. Server-rendered `Update preview` buttons provide the same preview without JavaScript and perform no writes. Supported prices display as currency; excess-precision entered prices remain exact in the server preview and are rejected on save.

The record shows the effective price per saved unit. A unit change leaves every stored numeric price and date intact. Preview shows, for example, `Resulting price: €4.80 per kg`, with `Price basis changes from €4.80 per Each to €4.80 per kg. The amount stays the same.` The flag appears for a changed unit basis and hides when the basis is unchanged or a new product has no prior price basis.

## Acceptance verification

| Quoted criterion | Evidence |
|---|---|
| Unit kg, Quantity Step 0.5, Minimum 1.0 saves; valid quantities are 1.0, 1.5, 2.0 kg and so on. | `Should_SaveMeasuredProductWithPricePerUnit_When_ValidRulesAreEntered` saves via the website and validates those quantities through the reloaded product's shared rule. The parameterised test also covers litre and metre. |
| Unit kg, Step 0.5, Minimum blank saves with Minimum 0.5. | `Should_DefaultMinimumToStep_When_MinimumIsBlank` covers both creation and existing-record editing, verifying persisted 0.5 and valid minimum quantities. |
| Step 0.5, Minimum 0.7 is rejected with “Minimum must be a multiple of the step”. | `Should_RejectMinimum_When_NotAMultipleOfStep` checks the exact field error for create and edit, retained inputs, and no change to records/prices. |
| Unit Each hides step and minimum; valid quantities are whole numbers of 1 or more. | `Should_ClearMeasuredRulesAndHideInputs_When_ProductReturnsToEach` verifies disabled/hidden fields, null stored measure fields and the shared whole-positive rule. Script tests verify immediate switching in both directions. |
| For a kg product the base price is entered and shown as “€4.80 per kg”. | The measured-create scenario checks entry wording, resulting price before save and the saved record. `Should_PreviewAndFlagPriceBasisWithoutSaving_When_UnitChanges` verifies the user-approved change flag, unchanged price entries and an unchanged-basis preview without a flag. |
| Reusable quantity validation reports “Enter a quantity of 1 or more” for Each and “Enter at least 1.0 kg in steps of 0.5 kg” for the example measure product. | The 40 `QuantityTests` cases check these exact messages, bounds, multiple units, zero stock counts versus orders, captured units, quarter/tenth steps and culture independence. |

Further checks verify the predecessor database upgrade, exact six-decimal persistence, SQL rejection of an invalid minimum, unchanged records after invalid API submissions, host restart, direct API authorization, removed-role/expired-session writes without replay, and an unrounded excess-price-precision preview followed by rejection on save. The domain test `Should_RejectUnitChange_When_ProductHasOrders` proves the ordered-product guard before any field or price changes.

## Verification results

- Existing product characterisation before changes: 11 API and 19 website cases passed.
- Shared quantity checkpoint: 40 cases passed; build had zero warnings/errors.
- Product/domain and SQL checks: new unit-change guard, returning to Each, predecessor upgrade and exact persistence passed.
- New website unit scenarios: all 23 passed in the final suite, including the exact-price-preview case.
- Final solution build: zero warnings and errors.
- JavaScript: 34 passed (18 identity/admin UI, 11 delivery status, 5 unit-form presentation), none skipped.
- Complete .NET regression suite: 1,139 passed (67 API, 96 website, 976 identity/admin), none failed or skipped.

## Scope and remaining dependencies

There is currently no order-storage model. The implemented and tested domain guard rejects unit changes when `hasOrders` is true. The API currently supplies false because there are no order records; real order-usage integration must be supplied when order storage is introduced. Historical-order unit changes belong to WI-068. Tablet controls, tablet runtime, quantity breaks and dated-price editing remain separate tasks; this slice does not select a tablet technology or implement those consumers.

All required plan placeholders are resolved. Quantity precision and range, zero stock counts, positive order quantities, and unchanged numeric prices with a flagged changed basis are developer-approved policies. No unresolved decision blocks this slice.

## Changed-file map

- `FieldSales.Quantities/` — shared unit/quantity values and order validation.
- `FieldSales.slnx`, API/API test/website project files — shared-library references.
- `FieldSales.Api/Catalogue/Product.cs`, `ProductEndpoints.cs`, `CatalogueEndpoints.cs`, `CatalogueDbContext.cs` and migration files — persistence, guard, protected operations and read projections.
- `FieldSales.Api.Tests/QuantityTests.cs`, `ProductTests.cs`, `ProductPersistenceTests.cs` — rules, guard and database evidence.
- `FieldSales.Web/Catalogue/CatalogueApiClient.cs`, `ProductUnitFields.cs`, product Create/Detail pages and `_UnitFields.cshtml` — BFF, entry/edit forms and server preview.
- `FieldSales.Web/wwwroot/js/product-unit-form.js`, `wwwroot/css/site.css` — immediate preview and field visibility.
- `FieldSales.Web.Tests/ProductUnitEndToEndTests.cs`, `product-unit-form.test.mjs` — accepted flow and script checks.
- This evidence record and `plan_docs/field-sales-delivery/plan-data.js` — policies and status only.

The authorized `plan-data.js` status map marks WI-007 done; no card content has changed. The feature branch is `feature/wi-007-set-unit-measure-step-minimum`. The quantity contract is the first buildable milestone; all approved work and verification are complete. WI-008 is the next item in console order, informational only; it has not been started.
