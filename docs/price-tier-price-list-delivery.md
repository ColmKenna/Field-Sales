# WI-061 — Create price-list tiers and warn when a price can never apply

T-6.1.2 / Pricing US-001 S3–S5; uxdocs 02 H-20. Feature branch: `feature/wi-061-create-price-list-tiers-warn`.

A Head Office User can now set a tier up as a price list ("Suncare Deal 2027"): fixed prices for named products, with every other
product at base price. A fixed price above today's list price is saved but flagged "This is above list price and will never apply".
A tier can change shape, and a confirm step says what stops applying before anything changes. As in WI-060, a tier stores its terms
only; the price engine (WI-058), which already had a price-list tier, works out what it gives.

## Decisions

The developer answered four questions and replied **Approved** to D1–D9, the steps and scenarios C1–C4 and 1–12, then **Apply**.
At the checkpoint the developer replied "Proceed", taken as `Continue T-6.1.2`.

| # | Decision |
|---|---|
| D1 | **Percentage → price list.** Fixed overrides carry over as listed prices. Percentage overrides are removed, and the confirm step names them. The default percentage is cleared. |
| D2 | **Confirm step.** "Change to a price list…" / "Change to a percentage tier…" opens a page that says what stops applying, with Confirm and Cancel. Nothing changes until Confirm. |
| D3 | **Above-list warning.** A fixed price saves straight away. While it is above today's list price (the business day in Ireland), its row says "This is above list price and will never apply." This covers percentage-tier fixed overrides and price-list entries alike. There's no warning when the price equals list (it ties, and the tier shows), or when the product has no list price today (inferred). It's worked out on reading, never stored. |
| D4 | **Price list → percentage.** Head office enters the new default percentage on the confirm step; listed prices stay as fixed overrides. |
| D5 | **Storage (inferred).** A `Shape` column on `PriceTiers` (`Percentage` / `PriceList`). `PercentOff` is nullable, and the check `CK_PriceTiers_Terms` requires a percentage on percentage tiers and none on price lists. Price-list entries are `PriceTierOverrides` rows with a fixed price; the domain refuses a percentage entry on a price list. One migration, `AddPriceListTiers`; existing tiers become percentage tiers. |
| D6 | **API (inferred).** Creating a tier takes a shape; with none it's a percentage tier, so WI-060's request still works. A price list refuses a percentage. `PUT /pricing/tiers/{id}/shape` changes shape. The list and detail carry the shape, and each detail row an above-list flag. Access and last-save-wins are unchanged. |
| D7 | **Pages (inferred).** The create form chooses the shape; the percentage box is for percentage tiers only, and a price list ignores it. The list reads "Suncare Deal 2027 · price list · 20 products" beside "Tier B · 8% off base · 2 overrides". A price list's detail says "Listed prices", offers a fixed price only, and reads "No listed prices yet. Every product is at base price." when empty. |
| D8 | **Out of scope.** Drift indicators (E19/WI-157), assigning tiers and customer counts (T-6.2.1), category overrides (MI-47), deleting tiers. |
| D9 | **Finish.** As WI-060 if the developer says so at the time. |

## What was built

- **Model** (`src/FieldSales.Api/Pricing/PriceTier.cs`):
  - `PriceTierShape`; `PriceTier.Shape`; `PercentOff` is now nullable.
  - `CreatePriceList`, `ChangeToPriceList` (returns the products whose percentage overrides went), `ChangeToPercentage`.
  - `Change` takes no percentage on a price list; `SetOverride` takes fixed prices only on a price list.
  - `ToEngine()` gives the engine's `PercentageTier` or `PriceListTier`.
  - `PriceTierOverride.IsAboveList(listPrice)`.
- **Storage:** `PriceTierModelConfiguration.cs` and the migration `20261106154730_AddPriceListTiers`: the `Shape` column (existing rows
  `Percentage`), nullable `PercentOff`, and `CK_PriceTiers_Terms` in place of `CK_PriceTiers_PercentOff`.
- **Contracts** (`src/FieldSales.Catalogue.Contracts/PriceTiers.cs`): `PriceTierShapes`; `Shape` on `PriceTierListItem`,
  `PriceTierDetails` and `SavePriceTierRequest` (create only); `AboveList` on `PriceTierOverrideItem`; `ChangePriceTierShapeRequest`.
- **API** (`PriceTierEndpoints.cs`): create by shape; change within the current shape; `PUT /pricing/tiers/{id}/shape`; the
  above-list flag on detail rows, judged against today's list price.
- **Website:**
  - `CatalogueApiClient.PriceTiers.cs`: `CreatePriceTierAsync` takes a shape; `ChangePriceTierShapeAsync`.
  - `/HeadOffice/Tiers`: shape choice on the create form; each tier's shape in the list.
  - `/HeadOffice/Tiers/{id}`: "Listed prices" and fixed-price-only forms on a price list; the row warning; a "Change to …" link.
  - `/HeadOffice/Tiers/{id}/Shape` (new): the confirm step. Back to percentage asks for the percentage first, so the warning can
    name it ("Products not listed will get 8% off base.").
  - `TierText.Default`, `Named`, `Names`; a `.tier-warning` style.

## Criteria

| Criterion | How it is met |
|---|---|
| 1. "Suncare Deal 2027" as a price list of 20 products: those 20 resolve to their listed prices; all others fall back to Base Price | Scenario 1 (through SQL Server and the engine); 2, 12 |
| 2. A fixed price of €13.00 where base is €12.50 shows "This is above list price and will never apply", and can be saved or corrected | Scenarios 4, 5; the page for 4; 12 |
| 3. Changing Tier B from percentage to price list warns that the default percentage will no longer apply and unlisted products revert to base | Scenario 8; what's stored afterwards, 9; 12 |
| 4. The warning in 2 is evaluated against the current base price | Scenario 6 |

## Agreed scenarios

| # | Scenario | Where |
|---|---|---|
| C1 | `Should_PriceEveryProductAtTheDefaultPercentage_When_AStoredPercentageTierHasNoOverrides` | `tests/Integration/FieldSales.Api/PercentageTierCharacterisationTests.cs` |
| C2 | `Should_UseEachOverrideAndTheDefaultElsewhere_When_AStoredPercentageTierHasOverrides` | same |
| C3 | `Should_ReturnTheSameListAndDetail_When_HeadOfficeReadsAPercentageTier` | same |
| C4 | `Should_CreateAPercentageTier_When_TheRequestHasOnlyANameAndPercentage` | same |
| 1 | `Should_UseTheListedPricesAndBaseElsewhere_When_SuncareDeal2027IsAPriceListOf20Products` | `tests/Integration/FieldSales.Api/PriceListTierApiTests.cs` |
| 2 | `Should_CreateAPriceListTierWithNoPercentage_When_HeadOfficeChoosesPriceList` | same |
| 3 | `Should_RefuseAPercentage_When_AnEntryIsAddedToAPriceListTier` | same |
| 4 | `Should_SaveAndFlagTheRow_When_AFixedPriceOf13IsEnteredWhereBaseIs1250` | same, and `tests/Integration/FieldSales.Web/Catalogue/PriceListTierPageTests.cs` |
| 5 | `Should_ClearTheWarning_When_ThePriceIsCorrectedTo1050` | `PriceListTierApiTests.cs` |
| 6 | `Should_JudgeTheWarningOnTodaysBase_When_TheBasePriceRisesLater` | same |
| 7 | `Should_NotWarn_When_AFixedPriceEqualsBase` | same |
| 8 | `Should_WarnWhatStopsApplying_When_TierBIsChangingToAPriceList` | `PriceListTierPageTests.cs` |
| 9 | `Should_KeepFixedOverridesAsListedPricesAndRemovePercentOverrides_When_TheChangeIsConfirmed` | `PriceListTierApiTests.cs` |
| 10 | `Should_KeepListedPricesAsFixedOverrides_When_APriceListChangesBackToPercentage` | same, and `PriceListTierPageTests.cs` |
| 11 | `Should_ShowEachTiersShape_When_HeadOfficeOpensTheTierList` | `PriceListTierPageTests.cs` |
| 12 | `Should_CreateAPriceListFlagAndCorrectAPriceAndChangeTierBsShape_When_HeadOfficeUsesTheForms` | `tests/Integration/FieldSales.Web/Catalogue/PriceListTierIntegrationTests.cs` |

C1 and C2 write tiers with SQL in WI-060's schema, then run every later migration, so they pin both the pricing and the upgrade of
existing tiers. C3 and C4 read and send the API's JSON by property name, so the added properties don't disturb them. All four passed
before any production change and pass after it.

Edges from the scenario guidance:
- A price equal to base gives no warning, and the tier wins the tie (scenario 7). A product with no list price today gives none either (scenario 7, D3).
- Changing back from price list to percentage (scenario 10).
- "A product later archived" can't arise: products have no archived state yet.

WI-060's API tests now also expect the `Shape` and `AboveList` values, and its access scenario (9) also checks the shape endpoint.

## Mutation checks

Each check was run against the whole test project the scenario lives in.

| Mutation | Tests that failed |
|---|---|
| The migration's generated `Shape` default left as `""` (seen while building, before the fix) | C1, C2 |
| Creating a tier ignores the shape | 7 tests, including 3, 4, 5, 6, 7 and 10 |
| A price list accepts a percentage entry | 3 |
| A price equal to list is flagged | 7 |
| No list price today counts as €0.00 | 7 |
| The flag judged on the first base price, not today's | 6, 7 |
| A change to a price list keeps percentage overrides | 9 |
| A change back to percentage drops the listed prices | 10 |
| The engine reads a price list as a percentage tier | 8 tests, including 1, 3, 4, 5, 7 and 9 |
| The confirm step leaves out the overrides it removes | 8, 12 |
| A price list is sent the percentage typed on the create form | 11 |
| Every fixed-price row flagged | 4 (page), 12 |
| Back to percentage confirms without naming the percentage | 10 (page) |

The API mutations ran against the 230 API integration tests, the website ones against the 881 web integration tests.

## Verification

- `dotnet build FieldSales.slnx -c Release --no-incremental`: no warnings.
- `dotnet ef migrations has-pending-model-changes` (catalogue): no pending changes.
- Full suite (SQL Server): 3,369 passed, 0 failed (WI-060 ended at 3,351; plus C1–C4 and 14 new tests for scenarios 1–12).
- `node --test plan_docs/field-sales-delivery/tests/status-store.test.js`: 11 passed.
- Android Release build (`scripts/tablet-app.ps1 -Action Build -Configuration Release`): succeeded (the tablet is unchanged).

## Not done here

- **Display and storage only.** Nothing on the tablet or in order capture uses tiers yet; that comes with T-6.2.1, WI-063 and WI-064.
- **Drift** on fixed prices (WI-157) builds on each entry's `SetOn`.
- **The migration's Down step** can't run while price-list tiers exist, because their `PercentOff` is empty; remove them first.
