# WI-065 — Show where a line's price came from, with every candidate on demand

T-6.4.3 / Pricing US-005 S1–S4; Rep at a Location US-012 S4b; uxdocs 00 §1 (price provenance sheet). Feature branch:
`feature/wi-065-show-where-line-s-price`.

Each order line on the tablet now says where its price came from — "€11.20 — Tier B (list €12.50)", "€12.50 — list price",
"€11.00 — Break: 10+ (better than your tier price €11.50)" — and an (i) beside it opens a sheet with the price applied and every
other candidate considered. All of it is read from what the line captured (WI-064); nothing is worked out again.

## Decisions

The developer answered four questions, replied **Approved** to D1–D9, then **Apply**, and `Continue T-6.4.3` after the device
checkpoint.

| # | Decision |
|---|---|
| D1 | **MI-54: the runner-up tier price is on the line,** as the stories say (UX DECISION 1.1 not followed). It takes the place of the list price note, so a line carries at most one extra price. |
| D2 | **Line text** from the captured price only: list wins "€12.50 — list price"; a tier wins "€11.20 — Tier B (list €12.50)" (no list price that day: "€10.50 — Tier B"); a break wins with a tier among the candidates "… (better than your tier price €11.50)", with none "… (list €12.50)"; measured products per unit. |
| D3 | **(i)** is its own button beside the price, named "Price details for {product}" (§1 DECISION 1.2). |
| D4 | **The sheet:** "Price for {product}"; APPLIED (price and source); ALSO CONSIDERED (every other candidate, in the engine's order, named as the line captured them; a list price as "List price"); "Nothing else was considered." when there was one candidate. |
| D5 | Source and status are always in words. |
| D6 | The line total and quantity read as before. |
| D7 | **Order lines only;** pad and search rows keep just their price. |
| D8 | **Out of scope:** promotions and the offer summary (E18), rep prices (E19), break prompts (T-6.5.1, WI-066), (i) on pad rows. |
| D9 | **Finish:** as before, if the developer says so. |

## What was built

- `src/FieldSales.Tablet.Core/LinePriceText.cs`: `Summary` (the line's sentence from its captured price), `Name` (a candidate's
  name in the sheet), `Money` (with "per kg" for measures).
- `src/FieldSales.Tablet/Components/Shared/PriceSheet.razor`: the provenance sheet; styles in `wwwroot/app.css`.
- New Order's lines show `LinePriceText.Summary` and an "(i)" button that opens the sheet.
- Tests that pin a read-only order's buttons now include "(i)", which only explains the price (4 places). The fake order store in
  `NewOrderComponentTests` can capture other than the base price.

## Criteria

| Criterion | How it is met |
|---|---|
| 1. A line where Tier B wins reads "€11.20 — Tier B (list €12.50)" | Scenario 1 (unit and page); on the tablet, "€8.51 — Tier B (list €9.25)" |
| 2. A line where base wins reads "€12.50 — list price" with no runner-up | Scenario 2 (unit and page), including lines captured before WI-064 |
| 3. A line where a break wins shows its source "Break: 10+" | Scenarios 3–5; on the tablet, "€2.80 — Break: 10+ (better than your tier price €2.94)" |
| 4. The (i) marker opens a sheet with APPLIED and ALSO CONSIDERED, matching the §1 frame | Scenarios 6, 8; on the tablet (screenshot) |
| 5. The runner-up's place follows the MI-54 decision | Decided by the developer: on the line (D1); scenario 3 |

## Agreed scenarios

| # | Scenario | Where |
|---|---|---|
| C1 | `Should_ShowTheCapturedUnitPriceAndTotal_When_ALineIsOnTheOrder` | `tests/Integration/FieldSales.Tablet.Components/NewOrderComponentTests.LinePriceCharacterisation.cs` |
| C2 | `Should_ShowTheCapturedPrice_When_TheShopsPriceHasChangedSince` | same |
| 1 | `Should_ReadTheTierWithTheListPrice_When_TierBWins` | `tests/Unit/FieldSales.Tablet.Core/LinePriceTextTests.cs`; `NewOrderComponentTests.PriceSource.cs` |
| 2 | `Should_ReadListPriceWithNoRunnerUp_When_BaseWins` | both |
| 3 | `Should_ShowTheBreakAndTheTierAsRunnerUp_When_ABreakBeatsTheTier` | both |
| 4 | `Should_ShowTheBreakWithTheListPrice_When_NoTierPricesTheProduct` | `LinePriceTextTests.cs` |
| 5 | `Should_ShowPerUnitPrices_When_TheProductIsMeasured` | same |
| 6 | `Should_OpenTheSheetWithAppliedAndAlsoConsidered_When_TheMarkerIsTapped` | `tests/Integration/FieldSales.Tablet.Components/PriceSheetScreenTests.cs` |
| 7 | `Should_ListTheEqualCandidateUnderAlsoConsidered_When_TwoCandidatesTie` | `LinePriceTextTests.cs` |
| 8 | `Should_SayNothingElseWasConsidered_When_TheLineHadOneCandidate` | `PriceSheetScreenTests.cs` |
| 9 | `Should_NameTheMarkerAndTheSourceInWords_When_ReadWithoutColour` | `NewOrderComponentTests.PriceSource.cs` |

The sheet tests (6, 8) use the session screen's renderer, which renders child components; the New Order test renderer reads only
the page's own frames.

## Device check (checkpoint)

On the Samsung tablet (`R52T30AZZXZ`) with the demo AppHost: "Demo Northside Retail" (Swords Shop) given Tier B (8%) and Demo Hand
Soap a break from 10 at €2.80 in the demo catalogue. After a sync, the pad showed €8.51 and €2.94, quantity entry quoted €2.80 at 12,
the lines read as above and the hand soap's (i) opened "Price for Demo Hand Soap" with APPLIED €2.80 Break: 10+ and ALSO CONSIDERED
€2.94 Tier B, €3.20 List price. Observed: a long line text pushes that line's quantity and total left, so columns don't line up
between lines; left as is.

## Mutation checks

Each check was run against the whole test project the scenario lives in (tablet unit tests 56, tablet component tests 229).

| Mutation | Tests that failed |
|---|---|
| No runner-up: always the list price | 3 |
| The list price note dropped | 1, 4, 5, 7 |
| A list winner reads its captured label | 2 |
| Measured prices lose their unit | 5 |
| The sheet names a list price as captured | 7 |
| The winner listed again under also considered | 8 |
| Also considered most expensive first | 6 |
| The line shows only its amount | 1, 2, 3, 6, 9 (page) |
| The marker has no name | 6, 8, 9 |

## Verification

- `dotnet build FieldSales.slnx -c Release --no-incremental`: no warnings.
- Full suite (SQL Server): 3,421 passed, 0 failed (WI-064 ended at 3,407; plus C1–C2 and 12 tests for scenarios 1–9).
- `node --test plan_docs/field-sales-delivery/tests/status-store.test.js`: 11 passed.
- Android Release build (`scripts/tablet-app.ps1 -Action Build -Configuration Release`, from clean output): succeeded.

## Not done here

- **Column alignment** between lines with long and short price text (seen on the device).
- **(i) on pad rows** (D7) and promotions in the sheet (E18).
