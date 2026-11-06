# WI-063 — Assign tiers to a customer with a sample of resulting prices

T-6.2.1 / Pricing US-003 S1–S4; uxdocs 02 H-21 (H21.1, H21.2), H-25 (H25.2). Feature branch:
`feature/wi-063-assign-tiers-customer-sample-resulting`.

A Head Office User ticks the tiers a customer holds on its record, sees a sample of the prices those tiers give before saving, and
every Location of the customer then prices against them, with the best price winning per line.

## Decisions

This is the second of the three items the developer asked for with approval assumed ("do the next 3 items, assume approval instead
of asking"). The decisions are the recommended option at each point where a question would have been asked; the plan, scenarios,
Apply and the checkpoint (`Continue T-6.2.1`) were taken as approved.

| # | Decision |
|---|---|
| D1 | **Link storage.** A table `CustomerPriceTiers` (customer, tier) in the catalogue database beside the tiers, with a foreign key to the tier. The customer lives in the Directory database, so it is checked there on save rather than by a foreign key. |
| D2 | **Editing (H21.1).** A "Pricing" section on the customer record: "Tiers: Tier B, Suncare Deal 2027" as saved (or "Tiers: none — Base Price plus any break"), a checkbox per tier with its terms, "Update sample" (prices the ticked tiers without saving) and "Save tiers". The set replaces what the customer held; a tier sent twice is held once. |
| D3 | **Sample (H21.2).** Rows "Product · Base · This customer · From", priced by the engine for today at the product's smallest orderable quantity, with the ticked tiers. The products are the 5 on the most of the customer's last 20 accepted orders (ties by name), plus "Check another product" (first 5 matches). |
| D4 | **Locations price against their customer's tiers.** One service, `CustomerTierPricing`, finds a Location's customer, its tiers and the engine inputs. The sample uses it, and WI-064's snapshot will. |
| D5 | **Removing a tier** changes prices from then on only. Captured orders keep their captured prices; the server never re-prices. |
| D6 | **Access.** The `HeadOfficePricing` API policy; the customer record is under `/HeadOffice` (Head Office User). |
| D7 | **Out of scope.** Tiers on the tablet (T-6.4.2, WI-064), promotions by tier (E18), drift (E19), "Held by N customers" on the tier list (H20.3). |
| D8 | **Finish.** Push, PR, merge after CI (merge commit, branch kept), update local main. |

## What was built

- **Model and storage:** `CustomerPriceTier` and `CustomerTierPricing` (`src/FieldSales.Api/Pricing/CustomerTierPricing.cs`);
  configuration beside the tiers and the migration `AddCustomerPriceTiers`.
- **API** (`CustomerTierEndpoints.cs`, policy `HeadOfficePricing`):
  - `GET /pricing/customers/{id}/tiers`: the tiers held and every tier to choose from.
  - `PUT /pricing/customers/{id}/tiers`: replaces the set; an unknown customer is 404, an unknown tier 400.
  - `GET /pricing/customers/{id}/sample?tierId=…&find=…`: the most-ordered products and any matches, priced with the tiers given.
- **Contracts** (`src/FieldSales.Catalogue.Contracts/CustomerPricing.cs`): `CustomerTiersDetails`, `SetCustomerTiersRequest`,
  `CustomerPriceSample`, `CustomerPriceSampleRow`.
- **Website:** `CatalogueApiClient.CustomerTiers.cs`; the Pricing section on `/HeadOffice/Customers/Detail/{id}`. If pricing can't be
  read, the section says so and the rest of the record still shows.
- **Test support:** the directory web test fixture (`GeographyApplication`) gives the API catalogue and ordering databases of its
  own, since the customer record now reads them.

## Criteria

| Criterion | How it is met |
|---|---|
| 1. Assigning Tier B and "Suncare Deal 2027" to Hickey's Pharmacies makes all 13 of its Locations price against both, best price winning per line | Scenario 1 |
| 2. The Customer shows "Tiers: B, Suncare Deal 2027" and a sample of products with the price each would get, recalculated as tiers are ticked, before saving | Scenarios 2, 3, 6 and 11 |
| 3. Removing "Suncare Deal 2027" affects new orders only; captured orders keep their prices | Scenario 4 |
| 4. A Customer with no tier prices at Base Price plus any break | Scenario 5 |
| 5. The sample shows the customer's most-ordered products and lets the user search for any other | Scenarios 6, 7 |

Tier names show as stored, so the record reads "Tiers: Suncare Deal 2027, Tier B" (by name) where the story writes "B".

## Agreed scenarios (assumed)

| # | Scenario | Where |
|---|---|---|
| 1 | `Should_PriceEveryLocationAgainstBothTiers_When_HickeysHoldsTierBAndSuncareDeal2027` | `tests/Integration/FieldSales.Api/CustomerTierPricingTests.cs` |
| 2 | `Should_ShowTheTiersAndASampleOfPrices_When_HeadOfficeOpensTheCustomer` | `tests/Integration/FieldSales.Web/Catalogue/CustomerTierPageTests.cs` |
| 3 | `Should_RecalculateTheSampleWithoutSaving_When_TiersAreTicked` | same |
| 4 | `Should_PriceNewOrdersWithoutTheTierAndKeepCapturedPrices_When_SuncareDeal2027IsRemoved` | `CustomerTierPricingTests.cs` |
| 5 | `Should_PriceAtBasePlusAnyBreak_When_TheCustomerHoldsNoTier` | same |
| 6 | `Should_SampleTheMostOrderedProducts_When_TheCustomerHasAcceptedOrders` | same |
| 7 | `Should_PriceASearchedProduct_When_HeadOfficeChecksAnother` | same |
| 8 | `Should_HoldEachTierOnce_When_TheSameTierIsSentTwice` | same |
| 9 | `Should_RefuseTheTiers_When_TheCustomerOrATierIsUnknown` | same |
| 10 | `Should_RefuseTierAssignment_When_TheCallerIsNotAHeadOfficeUser` | same |
| 11 | `Should_TickSampleAndSaveTiers_When_HeadOfficeUsesTheCustomerRecord` | `tests/Integration/FieldSales.Web/Catalogue/CustomerTierIntegrationTests.cs` |

Edges from the scenario guidance: the same tier twice is held once (scenario 8); a customer with 13 Locations (scenario 1); removing a
tier while a tablet's snapshot still has it can't arise yet, because tiers aren't in the snapshot until WI-064.

## Mutation checks

Each check was run against the whole test project the scenario lives in.

| Mutation | Tests that failed |
|---|---|
| A Location prices with only its customer's first tier | 1, 6 |
| Pending orders count in the sample | 6 |
| A product counted once per line, not once per order | 6 |
| The sample ignores the ticked tiers | 6, 7 |
| Saving keeps tiers no longer sent | 4 (first run: survived; see below) |
| Unknown tiers accepted | 9 |
| "Update sample" saves | 3, 11 |
| The sample priced with the saved tiers, not the ticked ones | 3, 11 |
| "Tiers:" lists the ticked tiers, not the saved ones | 3, 11 |

"Saving keeps tiers no longer sent" survived the first run: only the website end to end test (11) unticks a tier, and scenario 4
removed the deal straight from the database. Scenario 4 now removes it the way the story says, by saving the customer's tiers
without it through the API, and the mutation is caught.

The API mutations ran against the 248 API integration tests, the website ones against the 887 web integration tests.

## Verification

- `dotnet build FieldSales.slnx -c Release --no-incremental`: no warnings.
- `dotnet ef migrations has-pending-model-changes` (catalogue): no pending changes.
- Full suite (SQL Server): 3,393 passed, 0 failed (WI-062 ended at 3,382; plus the 11 scenarios).
- `node --test plan_docs/field-sales-delivery/tests/status-store.test.js`: 11 passed.
- Android Release build (`scripts/tablet-app.ps1 -Action Build -Configuration Release`): succeeded (the tablet is unchanged).

## Not done here

- **The tablet doesn't price with tiers yet.** The snapshot carries them with WI-064, through `CustomerTierPricing`'s rules.
- **"Held by N customers"** on the tier list (H20.3) isn't shown.
