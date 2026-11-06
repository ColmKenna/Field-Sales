# WI-058 — Resolve the best price for a line at its quantity

T-6.4.1 / Pricing US-005 S2, S3, S5; US-003 S1, S4; US-004 S1, S2, S4. Design decisions "Best for the customer wins; nothing
compounds" and "Price is resolved at the line's quantity". Feature branch: `feature/wi-058-resolve-best-price-line-quantity`.

The price engine. Given a product, the tiers a Customer holds, a quantity and a date, one pure function returns the lowest
candidate price. It also returns where that price came from, the runner-up tier price and every candidate considered. The tablet
and the server run the same code, so they can't disagree.

This item builds the engine only. Tier and break setup (WI-060–062), tier assignment (WI-063) and showing prices on the Order Pad
and lines (WI-064, WI-065) come next and call it.

## Decisions

- The developer answered four questions and replied **Approved** to D1–D10, the three increments and the 19 scenarios, then **Apply**.
- The item is Human Tight-Loop: the developer reviewed each increment and replied `Continue T-6.4.1` before the next.

| # | Decision |
|---|---|
| D1 | **Rounding (MI-48).** A unit price (per item or per kg) is rounded to the cent, halves away from zero, as soon as a percentage is applied. A line's value is the rounded unit price × quantity, rounded the same way (`Money.LineValue`). |
| D2 | **Overrides (MI-47).** Per product only. A percentage tier has a default percentage and product overrides, each a percentage or a fixed price. A price-list tier has fixed prices; a product it doesn't list gets no tier price. There are no category overrides. |
| D3 | **Ties.** On an equal price the rep sees the tier, then the break, then the list price. Equal tiers are ordered by name, compared exactly character by character. Later sources rank after these until E18 decides (inferred). |
| D4 | **Breaks.** Every break at or below the quantity is reached, and the lowest-priced of them is the one break candidate. A rising break therefore never applies (US-004 S3). On equal prices the lower threshold is used. The rule is the same for Each and measured units. |
| D5 | **Output.** `ResolvedPrice` holds the candidates cheapest first, so the winner is the first; the runner-up tier; the list price on the day; and the unit. Each candidate has a kind, a label and an amount. The list price's kind is `"Base"`, as order lines already use, so the result maps onto `OrderLinePrice`. Labels are "List price", the tier name, and "Break: 10+" or "Break: 10 kg+". When no candidate exists, the result is null. |
| D6 | **Runner-up and summary.** The runner-up is the best tier price, shown only when the winner isn't a tier. Example summaries: "€11.20 — Tier B (list €12.50)", "€12.50 — list price", "€10.08 — Break: 24+ (better than your tier price €11.20)", "€4.00 per kg — Break: 10 kg+ (list €4.80 per kg)". |
| D7 | **Shared module.** `src/FieldSales.Pricing` has no I/O and no dependencies. `FieldSales.Tablet.Core` and `FieldSales.Api` reference it. With one implementation, the agreed scenarios are the fixed test set and no separate vectors file is needed. The tests are in `tests/Unit/FieldSales.Pricing`. |
| D8 | **Inputs.** `PriceResolver.Resolve(product, tiers, quantity, date, extraCandidates = null)`. The date is the caller's calendar day; MI-53 (the business time zone) belongs to WI-059. Promotions (E18) arrive as extra candidates. Rep allowances (E19) adjust the result afterwards and add their working. The signature doesn't change for either. |
| D9 | **Out of scope.** Tier and break setup (WI-060–062), tier assignment (WI-063), display (WI-064, WI-065), break prompts (WI-066), promotions (E18), allowances (E19), the time zone (WI-059). |
| D10 | **Finish.** Push, PR, merge after CI (merge commit, branch kept), update local main. |

## The engine

- **Inputs** (`PricingInputs.cs`):
  - `PricedProduct(Id, Unit, ListPrices, Breaks)`, with `ListPriceOn(date)`;
  - `PercentageTier(Name, PercentOff, Overrides)`, whose overrides are `TierOverride.Percent` or `TierOverride.Fixed`;
  - `PriceListTier(Name, Prices)`.
- **Candidate generators** (`PriceCandidates.cs`), the card's delegable parts, built in increment 1:
  - `TierCandidates.For` gives one candidate per tier that prices the product;
  - `BreakCandidate.For` gives the lowest-priced break reached;
  - `Money.ToCent` and `Money.LineValue` do the rounding.
- **Resolver** (`PriceResolver.cs`), built in increment 2. It sorts the candidates by amount, then by kind rank (tier, break, list,
  others), then by tier name. The sort is stable, so extra candidates keep the order they were supplied in.
- **Bad input:** a quantity of zero or less is rejected with `ArgumentOutOfRangeException`. The screens validate quantities before
  pricing.
- **Offline (US-005 S5).** The engine is pure and ships inside the tablet's `Tablet.Core`, so resolving a price needs no server.
  Tiers and breaks reach the snapshot with WI-063 and WI-064.

## Criteria

| Criterion | How it is met |
|---|---|
| With no promotion, "€11.20 — Tier B (list €12.50)" (US-005 S2) | Scenario 1 |
| With no tier or promotion, "€12.50 — list price" with no runner-up (S3) | Scenario 2 |
| Offline, resolution on the tablet from the snapshot, including every tier (S5) | By design: pure library in `Tablet.Core`; the Android build compiles it. The tier data arrives with WI-063 and WI-064. |
| Tier B and "Suncare Deal 2027": the best of both per line; no tier: base plus any break (US-003 S1, S4) | Scenarios 3, 4 |
| Throat Lozenges: 12 at €2.00, 30 at €1.75; Loose Herbal Tea: 12 kg at €4.00 per kg (US-004 S1, S2) | Scenarios 5, 6 |
| A tier price of €1.90 beats the €2.00 break at 12 (US-004 S4) | Scenario 7 |

## Agreed scenarios

All are in `tests/Unit/FieldSales.Pricing`: `PriceResolverTests` for 1–18 and `PriceResolverPerformanceTests` for 19.

| # | Scenario |
|---|---|
| 1 | `Should_ShowTierBAt1120WithList1250_When_TierBPricesSpf30At1120AndNoPromotion` |
| 2 | `Should_ShowListPriceWithNoRunnerUp_When_NoTierOrBreakApplies` |
| 3 | `Should_TakeTheBestOfBothTiers_When_TheCustomerHoldsTierBAndSuncareDeal2027` |
| 4 | `Should_PriceAtListPlusAnyBreak_When_TheCustomerHasNoTier` |
| 5 | `Should_Price12At200And30At175_When_ThroatLozengesHasBreaksFrom10And30` |
| 6 | `Should_Price12KgAt400PerKg_When_LooseHerbalTeaHasBreaksFrom10And20Kg` |
| 7 | `Should_ChooseTheTierAt190OverThe200Break_When_TwelveAreOrdered` |
| 8 | `Should_LetTheTierWinAt23AndTheBreakAt24_When_Spf30HasABreakFrom24` |
| 9 | `Should_NeverApplyTheRisingBreak_When_From30At230SitsAboveFrom10At200` |
| 10 | `Should_RoundEachUnitPriceToTheCentHalfUp_When_APercentageTierIsApplied` |
| 11 | `Should_UseTheProductOverrideOverTheDefaultPercentage_When_ATierHasOneForThatProduct` |
| 12 | `Should_OfferNoTierPrice_When_APriceListTierDoesNotListTheProduct` |
| 13 | `Should_ShowTheTierThenTheBreakThenTheList_When_CandidatesAreEqual` |
| 14 | `Should_UseTheListPriceInEffectOnTheDate_When_ThePriceChangesTomorrow` |
| 15 | `Should_NameTheBestTierAsRunnerUp_When_ABreakBeatsTheTier` |
| 16 | `Should_ListEveryCandidateCheapestFirstWithTheWinnerMarked_When_ThePriceIsResolved` |
| 17 | `Should_ResolveNoPrice_When_ThereIsNoListPriceOnTheDateAndNoFixedTierPrice` |
| 18 | `Should_LetAnExtraCandidateWinUnderTheSameRule_When_ALaterSourceSuppliesOne` |
| 19 | `Should_Resolve260ProductsWithinBudget_When_TheOrderPadPricesEveryProductAtQuantity1` (260 products × 3 tiers under 50 ms, inferred budget, on the build machine) |

Mutation checks (scenarios 1–18 passed first time, so each rule was broken on purpose):

| Mutation | Tests that failed |
|---|---|
| Banker's rounding instead of half up | 10 |
| Highest threshold reached instead of the lowest break | 9 |
| An equal break shown before the tier | 13 |
| Percentage tier price left unrounded | 10 |
| No runner-up named | 15, 18 |
| List price taken regardless of the date | 14, 17 |
| A product's percentage override ignored | 11 |
| Equal tiers in the order held, not by name | 13 |

## Verification

- `dotnet build FieldSales.slnx -c Release --no-incremental`: no warnings.
- Full suite (SQL Server): 3,332 passed, 0 failed, including Pricing unit 19.
- `node --test plan_docs/field-sales-delivery/tests/status-store.test.js`: 11 passed.
- Android Release build (`scripts/tablet-app.ps1 -Action Build -Configuration Release`): succeeded, packaging `FieldSales.Pricing.dll` into the tablet app.

## Not done here

- **No screen uses the engine yet.** Order lines still capture the base price, labelled "Base price". The engine's label is
  "List price", so new lines will read that once WI-064 and WI-065 wire it in.
- **Not measured on a tablet.** The performance budget was measured on the build machine only.
- **Where a promotion ranks on a tie is an assumption.** E18 should confirm or change it.
