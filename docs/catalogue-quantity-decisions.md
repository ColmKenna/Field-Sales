# Product quantities — WI-007

Date: 2026-09-30. Source: T-1.4.1 / PRD-US-003 and PRD-US-002 S5. Status: active, at the quantity-contract review checkpoint.

## Approved plan and policies

The developer approved the plan and scenario list before implementation, including these refinements:

- Use exact decimals with precision 18 and scale 6. The maximum is `999999999999.999999`; values outside the range or requiring more fractional precision are rejected. There is no rounding or floating-point path.
- Stock counts may be zero. Order quantities remain positive and must satisfy the product's minimum and step. Free-of-charge items use a positive quantity and a zero price; that feature belongs to WI-156, as confirmed by `plan_docs/stories/pricing.md`.
- Support Each, kg, litre and metre. Each orders are whole numbers of one or more. Measures require a positive step; a blank minimum defaults to that step, and a supplied minimum must be a positive multiple of it.
- When a product's unit changes, retain its numeric base-price amount. Before saving, show the resulting price and flag its changed unit basis, for example `€4.80 per Each → €4.80 per kg`. This website behaviour follows checkpoint approval.

Existing product persistence and website behaviour was characterised first: 11 API product/domain persistence cases and 19 product website/API/SQL cases passed.

## Quantity contract for review

`FieldSales.Quantities` is a dependency-free .NET 10 library. API and unit tests reference it. It has no EF Core, ASP.NET, tablet-runtime, authentication or price dependencies.

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

The tests use the approved scenario intents. The tests for persistence, price preview and form access remain for the implementation after this checkpoint.

## Remaining work after review

Extend Product and its protected endpoints with quantity rules, add the step/minimum migration, update BFF DTOs and product Create/Detail pages, implement the price preview and change flag, then run the agreed end-to-end cases and complete regression suites. Preserve current Each records, price history and category browsing.

There is currently no order-storage model. A unit-change domain guard for products with orders remains in the next implementation portion; real order-usage integration must be supplied when order records exist. Historical-order unit changes belong to WI-068. Tablet controls, tablet runtime, quantity breaks and dated-price editing remain separate tasks; this checkpoint does not select a tablet technology or implement those consumers.

The source checkpoint asks: “Is this the one quantity type the tablet, the order record and stock counts will all use, with no rounding path?” Review the contract and tests before persistence/UI work. Resume trigger: `Continue T-1.4.1`.

The authorized `plan-data.js` status map marks WI-007 active; no card content has changed. The feature branch is `feature/wi-007-set-unit-measure-step-minimum`. The quantity contract is the first buildable milestone; the completed branch is pushed only after all approved work and verification finish.
