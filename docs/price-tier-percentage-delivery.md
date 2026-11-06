# WI-060 — Create a percentage tier with per-product overrides

T-6.1.1 / Pricing US-001 S1, S2; uxdocs 02 H-20 (H20.1). Feature branch: `feature/wi-060-create-percentage-tier-per-product`.

A Head Office User creates a percentage tier ("Tier B, 8% off base"), then gives named products their own term, either a different
percentage or a fixed price. A tier stores its terms only. The price engine (WI-058) works out what it gives from each product's
base price history, so a base price change flows through with nothing to update.

## Decisions

The developer answered four questions and replied **Approved** to D1–D11, the steps and the 12 scenarios. Their next reply was
`Continue T-6.1.1`, sent before anything was built; it was taken as **Apply**, and work stopped at the checkpoint as planned. After
reviewing the storage model there, the developer asked for the item to be finished, merged and local main updated.

| # | Decision |
|---|---|
| D1 | **Category override (MI-47): not built.** WI-058 settled per-product overrides only, so S2's "Suncare products at 15%" isn't built. Criterion 5 is reported as decided against. |
| D2 | **Storage holds terms only.** `PriceTiers` holds the name and default percentage. `PriceTierOverrides` holds the tier, the product, and either a percentage or a fixed price, one per product per tier. No column holds a resulting price. |
| D3 | **Where (inferred).** In the catalogue database beside Products, with a foreign key from each override to its product. Restriction permissions already sit beside their groups the same way. |
| D4 | **Limits.** Percentages must be more than 0 and less than 100, to 2 decimal places. Fixed prices must be more than €0.00, to the cent. Tier names are required, unique ignoring case, and at most 100 characters (inferred). The rules are checked in the domain and again by database constraints. |
| D5 | **Editing.** Head office can rename a tier, change its percentage, and add, change or remove overrides. Tiers can't be deleted. When two edits clash, the last save wins (inferred). |
| D6 | **Override rows.** Each row shows its term, today's list price and, for a percentage, what the tier gives today: "Hand Cream 75ml · 15% off · €3.40 today (list €4.00)" and "SPF30 Sun Lotion 200ml · fixed €10.50 · list €12.50". Both prices are worked out through the engine on reading, for the business day in Ireland, and never stored. The detail page leads with overrides (H20.1). |
| D7 | **Picking.** Search on the tier page using the existing product search, showing the first 20 matches. Products already overridden are marked "Already has an override on this tier." |
| D8 | **For WI-157 (inferred).** Each override records the business day it was last set (`SetOn`). That's a date, not a price, so drift can later be measured against the base price history. |
| D9 | **Access.** API policy `HeadOfficePricing` (Head Office User plus the staff API scope). On the website, the `/HeadOffice` folder already requires Head Office User. The head office index links to "Price tiers". |
| D10 | **Out of scope.** Price-list tiers, the above-list warning and changing shape (T-6.1.2); assignment and customer counts (T-6.2.1); drift (WI-157); tiers in the snapshot; promotion audiences. |
| D11 | **Finish.** Push, PR, merge after CI (merge commit, branch kept), update local main. |

## What was built

- **Model** (`src/FieldSales.Api/Pricing/PriceTier.cs`):
  - `PriceTier` has `CreatePercentage`, `Change`, `SetOverride` (which replaces any existing override for that product), `RemoveOverride` and `ToEngine()`, which gives the engine's `PercentageTier`.
  - `PriceTierOverride` holds `PercentOff`, `FixedPrice` and `SetOn`.
  - `TierTerm` and `TierTerms` hold the limits and messages.
- **Storage:** `PriceTierModelConfiguration.cs` and the migration `AddPercentagePriceTiers`, with the checks `CK_PriceTiers_PercentOff`, `CK_PriceTierOverrides_Term` and `UX_PriceTiers_Name`.
- **Engine input:** `EngineInputs.ToPricedProduct()` passes a product's whole base price history to the engine.
- **API** (`PriceTierEndpoints.cs`):
  - `GET` and `POST /pricing/tiers`;
  - `GET` and `PUT /pricing/tiers/{id}`;
  - `PUT` and `DELETE /pricing/tiers/{id}/overrides/{productId}`.
- **Website:**
  - `CatalogueApiClient.PriceTiers.cs`;
  - pages `/HeadOffice/Tiers` (list and create) and `/HeadOffice/Tiers/{id}` (overrides, search, tier terms);
  - `TierText` for the wording.
- **Change from the plan:** the website calls go through a partial of the existing `CatalogueApiClient`, not a new `PricingApiClient`. That reuses its token handling, error parsing and the existing page-test fakes.
- **Test support:** the web test fixture `ProductApplication` now clears tiers before products, because overrides point at products.

## Criteria

| Criterion | How it is met |
|---|---|
| 1. Tier "B" as 8% off base prices every product 8% below its current base, checked through the engine | Scenario 1 |
| 2. A base price change flows through the tier automatically | Scenario 2 |
| 3. A fixed €10.50 override of SPF30 applies to that product; everything else uses 8% | Scenario 3, and 11 through the website |
| 4. A per-product override may be a percentage instead of a fixed price | Scenarios 3, 6 |
| 5. Category overrides only if MI-47 is decided in favour | Reported: MI-47 was decided per product only (WI-058 D2), so nothing was built |

"For customers holding Tier B": customers hold tiers from T-6.2.1. Until then the scenarios pass the stored tier straight to the
engine as the only tier held.

## Agreed scenarios

| # | Scenario | Where |
|---|---|---|
| 1 | `Should_PriceEveryProduct8PercentBelowItsCurrentBase_When_TierBIsCreatedAt8PercentOff` | `tests/Integration/FieldSales.Api/PriceTierPricingTests.cs` |
| 2 | `Should_FollowTheNewBase_When_ABasePriceRisesAfterTheTierIsSet` | same |
| 3 | `Should_UseEachOverrideAnd8PercentForEverythingElse_When_TierBOverridesSpf30At1050FixedAndHandCreamAt15Percent` | same |
| 4 | `Should_RefuseTheTerm_When_APercentageOrPriceIsOutsideTheLimits` | `tests/Integration/FieldSales.Api/PriceTierApiTests.cs` |
| 5 | `Should_RefuseTheName_When_AnotherTierHasItIgnoringCase` | same |
| 6 | `Should_KeepOneOverridePerProduct_When_AnOverrideIsSetAgain` | same |
| 7 | `Should_FallBackToTheDefault_When_AnOverrideIsRemoved` | same |
| 8 | `Should_PriceAtTheNewPercentage_When_TheTierIsRenamedAndChangedTo10Percent` | same |
| 9 | `Should_RefuseTiers_When_TheCallerIsNotAHeadOfficeUser` | same |
| 10 | `Should_ListTiersAndLeadTheDetailWithOverrides_When_HeadOfficeOpensPriceTiers` | `tests/Integration/FieldSales.Web/Catalogue/PriceTierPageTests.cs` |
| 11 | `Should_CreateTierBAndAddTheSpf30Override_When_HeadOfficeUsesTheForms` | `tests/Integration/FieldSales.Web/Catalogue/PriceTierIntegrationTests.cs` |
| 12 | `Should_MarkProductsThatAlreadyHaveAnOverride_When_SearchingToAddOne` | `tests/Integration/FieldSales.Web/Catalogue/PriceTierPageTests.cs` |

Edges from the scenario guidance:
- 0% and 100% are refused (scenario 4).
- A base price change flows through (scenario 2).
- Category overrides are reported as not built (D1).
- "An override on an archived product" can't arise: products have no archived state yet.

## Mutation checks

Each check was run against the whole test project the scenario lives in.

| Mutation | Tests that failed |
|---|---|
| Overrides not passed to the engine | 3 |
| Only a product's first base price passed to the engine | 2 |
| A fixed price read as a percentage | 3 |
| 100% allowed | 4 |
| Setting an override again adds a second one | 6 |
| Removing an override keeps it | 7 |
| A change of percentage ignored | 8 |
| Any staff member may manage tiers | 9 |
| Percentage rows show no price today | 10 |
| Products already overridden not marked | 12 |

## Verification

- `dotnet build FieldSales.slnx -c Release --no-incremental`: no warnings.
- Full suite (SQL Server): 3,351 passed, 0 failed (WI-059 ended at 3,339; plus the 12 scenarios).
- `node --test plan_docs/field-sales-delivery/tests/status-store.test.js`: 11 passed.
- Android Release build (`scripts/tablet-app.ps1 -Action Build -Configuration Release`): succeeded (the tablet is unchanged).

## Not done here

- **The engine type and the stored entity share a name.** `FieldSales.Pricing.PriceTier` is the engine type and
  `FieldSales.Api.Pricing.PriceTier` is the stored entity. Code that uses both imports the engine's types by alias.
- **Display only.** Nothing on the tablet or in order capture uses tiers yet; that comes with T-6.2.1, WI-063 and WI-064.
