# WI-044 — Browse the Order Pad by category with breadcrumbs

T-5.1.2 / REP-US-012 S1, S1b and the search non-functional note, from `main` at `3cd0458` on
`feature/wi-044-browse-order-pad-category-breadcrumbs`.

## Approved scope and decisions

The developer approved the plan, the 15-scenario list and decisions D1 to D8 (all as recommended) with
**Approved** on 2 November 2026, and resumed after the checkpoint with "continue", asking for the branch to
be pushed, opened as a PR and merged.

| # | Decision |
| --- | --- |
| D1 | Nested collapsible headers. Each header is a button with the full breadcrumb and "N products" beneath it. Opened: its subcategories first, then "In X (n)" with the products directly in it (T7.3 and S1b in one model). |
| D2 | Top-level categories start collapsed. Open headers stay open through search and adds, and reset when the screen closes or changes shop. |
| D3 | A pad row shows the quantity already on the order and **Add**, which opens WI-038's quantity entry beneath the row, empty (T7.7). Repeat adds merge (WI-038 D1). No − control: changing lines is WI-046. |
| D4 | Search matches name or code, as WI-038 approved. The breadcrumb in each result tells the two "Lotions" apart. |
| D5 | A category with no downloaded product beneath it is hidden, so a category holding only restricted products leaves no trace (T7.5). |
| D6 | Subcategories by name, ignoring case and accents; products by name, then code. |
| D7 | Sample catalogue: a new opt-in demo batch, `./scripts/demo-data.sh pad`. |
| D8 | Target device for the timing: the iPad mini (5th generation) paired with this Mac. |

## What was built

- `OrderPad` (Tablet.Core): built once per snapshot from its categories and products. Each header has its
  breadcrumb, its count beneath at every depth, its subcategories and its own products. Built without recursion,
  so depth is not limited by the stack; a category in a parent cycle is never reached from a top-level one.
- `ProductSearchIndex` now carries each product's category and the snapshot's categories, read in the same
  transaction as before, and exposes `Pad`. No snapshot contract, storage or schema change.
- `NewOrder.razor` opens on the pad when the search field is empty. The pad is rendered with recursive
  `RenderFragment`s inside the page, not a child component, so the page stays one render tree. Typing replaces
  the pad with results; clearing brings it back with the same headers open.
- Each search writes `product-search products=… matches=… ms=… handler-ms=…` under the `FieldSalesTiming` log
  tag, timed to the painted results in the WebView (`search-timing.js` now takes a result selector). No query
  text is logged.
- `app.css`: headers are full-width 56 px tap targets; breadcrumbs wrap rather than truncate; Add is 56 × 96 px.
- Demo `pad` batch (`DemoPadSeeder`, seed name `order-pad-v1`): 256 products in a five-level tree on top of the
  sample. Suncare has 4 subcategories and 12 products directly in it, 180 beneath; "Lotions" sits under Suncare
  and Body Care; `Health > Skincare > Suncare > Lotions > SPF50` is five levels deep.

## Acceptance criteria

| Criterion | How it is met | Result |
| --- | --- | --- |
| 1. New Order opens on the Order Pad: products by category with quantity controls and a search field; each product shows its breadcrumb. (Every product is unranged until E9.) | The pad holds every product in the active snapshot. Opening every header lists each product once with its code, breadcrumb, base price, quantity on the order and Add (scenarios 1, 11). | Pass |
| 2. Opening "Suncare" shows its 4 subcategories first, then "In Suncare (12)", and "180 products" as the total beneath. | Scenario 2 checks the header's "180 products", four subcategory headers by name, then "In Suncare (12)" and its 12 rows. A mutation that put products before subcategories made it fail. | Pass |
| 3. Search results show each product's breadcrumb, so "Lotions" under Suncare and under Body Care are distinguishable. | Scenario 3: "lotion 01" returns Body Lotion 01 under `Health > Skincare > Body Care > Lotions` and Sun Lotion 01 under `Health > Skincare > Suncare > Lotions`. | Pass |
| 4. Categories are collapsible headers. | Header buttons with `aria-expanded`; scenarios 7, 8 and 14. | Pass |
| 5. Search returns within 1 second for 260 products on a target device. | In-process: 0.35 ms first search and under 0.1 ms after, over 260 products (scenario 13 asserts under 1 s). The device timing on the iPad was **not taken**; the log line is in place for it. | Partial — device timing outstanding |
| 6. Restricted products never appear (they aren't in the snapshot). | Scenario 10 syncs a day with a restricted product in a private-only category, then the filtered day: neither the product nor the category appears on the pad or in search, although the older generation still holds them. | Pass |

## Scenarios and tests

| # | Scenario | Test |
| --- | --- | --- |
| 1 | Every downloaded product under its category with breadcrumb, price and Add | `NewOrderComponentTests.Should_ListEveryDownloadedProductUnderItsCategoryWithBreadcrumbPriceAndAdd_When_NewOrderOpens` |
| 2 | Suncare: 4 subcategories, then "In Suncare (12)", 180 products | `NewOrderComponentTests.Should_ShowFourSubcategoriesThenInSuncare12And180Products_When_SuncareIsExpanded`; counts also in `OrderPadTests.Should_ShowInGroupAfterSubcategories_When_ProductSitsOnBranchCategory` |
| 3 | Both "Lotions" told apart in search | `NewOrderComponentTests.Should_ShowEachResultsBreadcrumb_When_SearchMatchesProductsUnderBothLotionsCategories` |
| 4 | No "In" group on a category with only subcategories | `OrderPadTests.Should_ShowNoInGroup_When_CategoryHasOnlySubcategories` |
| 5 | Product on a branch category | `OrderPadTests.Should_ShowInGroupAfterSubcategories_When_ProductSitsOnBranchCategory` |
| 6 | Counts at five and six levels | `OrderPadTests.Should_CountEveryProductBeneathOnce_When_TreeIsFiveOrSixLevelsDeep` |
| 7 | Collapse and expand | `NewOrderComponentTests.Should_HideAndRevealChildren_When_HeaderIsCollapsedAndExpanded` |
| 8 | Top-level categories start collapsed (D2) | `NewOrderComponentTests.Should_StartWithTopLevelCategoriesCollapsed_When_PadOpens` |
| 9 | Empty categories hidden (D5) | `OrderPadTests.Should_HideCategory_When_NoProductsBeneathItAreDownloaded` |
| 10 | Restricted product and private category absent | `RestrictedTabletTests.Should_NeverListRestrictedProductOrPrivateCategory_When_RepLacksPermission` |
| 11 | Add from a pad row shows the quantity on the row (D3) | `NewOrderComponentTests.Should_AddLineAndShowQuantityOnRow_When_RepAddsFromPadRow` |
| 12 | Unpriced row: "No price yet", Add disabled | `NewOrderComponentTests.Should_ShowNoPriceYetAndDisableAdd_When_PadProductHasNoPriceToday` |
| 13 | Search over 260 products under a second | `OrderPadTests.Should_ReturnResultsWithinOneSecond_When_260ProductsAreSearched` |
| 14 | Clearing search restores the pad with headers kept | `NewOrderComponentTests.Should_RestorePadWithExpansionKept_When_SearchIsCleared` |
| 15 | Name order (D6) | `OrderPadTests.Should_OrderSubcategoriesAndProductsByName_When_CategoryIsExpanded` |

Also: `DemoSeedTests.Should_AddTheFiveLevelOrderPadCatalogueOnce_When_PadRunsAfterTheSample` checks the demo batch.
WI-038's existing New Order tests pass unchanged; the shared harness gained an optional catalogue and
`aria-label` lookup.

## Verification

- Fast tier (unit projects, Tablet.Core integration, Tablet components): all passed — Tablet.Core unit 27,
  Tablet.Core integration 238, Tablet components 91, Identity admin unit 338, API unit 247, Web unit 220.
- Demo script tests: 5 passed. iOS simulator Debug build of the tablet app: 0 warnings, 0 errors.
- Release build of `FieldSales.slnx` on this Mac: 0 warnings; one error, `CS0103` in
  `tests/Integration/FieldSales.TabletSpike/StorageScenarios.cs(172)`, which reproduces on a clean `origin/main`
  worktree and is not touched here.
- The SQL Server suites (including the new DemoData test) did not run locally: Docker Desktop hung on this Mac and
  would not restart. They run in CI on the pull request, which must be green before merge.
- Checkpoint render: the real page rendered with `HtmlRenderer` against the five-level, 260-product tree and
  screenshotted at iPad mini sizes (portrait 768 px, landscape 1024 px): Suncare opened, five levels opened, and
  search for "lotion 01".

## Not verified here

- The pad on the physical iPad and the device search timing (criterion 5's "on a target device"), and the
  standing, one-handed usability review. Open a shop's New Order after `seed` and `pad`, and read the
  `product-search` lines with `scripts/tablet-ipad.sh logs`.
- Android: no Android SDK on this Mac.

## Left for later items

Ranges, "Outside your ranges" and the Search all tab (E9), resolved price with its source (WI-064 / E6), the
measure stepper (WI-045), changing or removing lines and the − control (WI-046), the Low tab and tab strip
(WI-052), unavailable labels (WI-078), agreed-range sections (T7.14–T7.18).
