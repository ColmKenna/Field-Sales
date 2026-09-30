# Category browsing — WI-006

Date: 2026-09-30. Source: T-1.1.2 / PRD-US-005. The developer approved the implementation plan and scenario list before edits.

## Approved scope and interpretations

The category website follows WI-004's reviewed H-15 navigation: one category at a time, with its current breadcrumb linking to ancestors. Head Office User access remains enforced by the website and API, using the server-held BFF token.

“Beneath” is the total of products directly in the category plus every descendant. “Here” is only the direct count. This interpretation follows the H-15 sketch: 64 + 48 + 56 + 0 in child branches, plus 12 here, makes 180 beneath. Approval of the plan accepted this interpretation.

Category search matches a trimmed, case-insensitive substring of the category name. Results show the full current breadcrumb, link to the stable category ID, and sort by path. Blank website searches return root browsing; a nonblank query with no matches has an explicit empty state. These search defaults were inferred in the plan and accepted with its approval.

## Read contract and implementation

- `GET /catalogue/categories/` returns roots with `Here` and `Beneath` counts.
- `GET /catalogue/categories/{id}` returns the category, current breadcrumb, direct children with counts, and direct products ordered by code. Products contain only the existing ID, code, name, category ID and unit projection. Price history is not joined into counts.
- `GET /catalogue/categories/search?q=...` returns category IDs and full paths. An empty API query returns no search results. Search operates over the already loaded category hierarchy; it does not search product names or ancestor names.
- SQL groups product counts by category. `CategoryProductCounts` folds each leaf's total into its parent once, iteratively, without a depth cutoff or one query per category. It rejects incomplete or cyclic hierarchies rather than returning partial totals.
- Root and category pages show counts. The category page shows subcategories first, then product links beneath `In {category name}`. Existing create and rename forms remain available.

No persisted schema changed. Counts and breadcrumbs are derived on reads; there is no stored count or breadcrumb to refresh. Create and rename retain the predecessor's write rules. Adding a subcategory does not update product membership or price history.

## Acceptance evidence

| Quoted WI-006 criterion | Evidence |
|---|---|
| Opening “Suncare” (4 subcategories, 12 direct products, 180 beneath) shows “180 products beneath · 12 here”, then the 4 subcategories, then the 12 products under the heading “In Suncare”. | `Should_Show180Beneath12HereAndChildrenBeforeProducts_When_SuncareIsOpened` checks exact text, four child links before the heading, twelve product links, each code once, ordering and a product record link through the real website/API/SQL path. |
| Searching “Lotions” where it exists under Suncare and Body Care returns both, each with its full breadcrumb. | `Should_ShowBothFullBreadcrumbs_When_CategoryNamesMatchSearch` verifies both API and website results for exact, mixed-case/trimmed and partial queries. `Should_RefreshSearchBreadcrumbsAndKeepCounts_When_AncestorIsRenamed` verifies current paths and stable links after a website rename. |
| Adding subcategory “After Sun” to Suncare creates it and leaves the 12 products in Suncare. | `Should_KeepDirectProductsAndPrices_When_AfterSunIsAdded` creates the child through the website, checks its empty state and the parent's unchanged counts/rows, and compares every persisted product identity/code/category and every price entry before and after. |
| Counts include products at every depth beneath, counted once. | Unit tests cover populated branches, seventh-level leaves, sibling branches, unrelated roots, empty nodes and a 2,000-level chain. `Should_CountSixthLevelWithoutCountingPriceRows_When_ProductsHavePriceHistory` checks the real API, including an additional price entry that must not increase product counts. |

Additional end-to-end checks cover blank and unsuccessful searches, name-only matching, missing categories, and denied root/detail/search reads for rep, manager, removed-role and anonymous API callers. Existing category tests characterise create, rename, breadcrumbs, access removal and expiry before the change.

## Scope boundaries

Move, archive and recategorisation belong to E11. The parent story's inline “Recategorise products?” offer is explicitly deferred there; it is absent in this slice. Product search belongs to WI-013. Units, prices, lifecycle and additional product classifications retain their separate work items.

The delivery console is split into HTML and `plan-data.js`. The user's prior authorization makes `workItemStatus` authoritative; only that map changes for WI-006. The reviewed H-15 navigation from WI-004 resolves the conditional screen-review dependency. No unresolved blocker or placeholder remains.

## Changed files

- `FieldSales.Api/Catalogue/CatalogueEndpoints.cs` — protected category read projections and search.
- `FieldSales.Api/Catalogue/CategoryProductCounts.cs` — iterative subtree totals.
- `FieldSales.Api.Tests/CategoryProductCountsTests.cs` — count and depth tests.
- `FieldSales.Web/Catalogue/CatalogueApiClient.cs` — corresponding DTOs and BFF search call.
- `FieldSales.Web/Pages/HeadOffice/Categories/Index.cshtml` and `Index.cshtml.cs` — category search and root counts.
- `FieldSales.Web/Pages/HeadOffice/Categories/Detail.cshtml` — child counts and direct product links.
- `FieldSales.Web.Tests/StaffWebsiteFactory.cs` — existing category test fixture's expanded detail response.
- `FieldSales.Web.Tests/CategoryBrowseEndToEndTests.cs` — accepted scenarios through website/API/SQL.
- `plan_docs/field-sales-delivery/plan-data.js` — WI-006 status only.
- This decision and evidence record.

## Verification

- Before behaviour changes: 10 website category checks and 6 API category/domain persistence checks passed.
- WI-006 checks: 3 count unit tests and 11 end-to-end cases passed.
- Full solution build: zero warnings and errors.
- JavaScript suites: 18 identity/admin UI and 11 delivery status tests passed, none skipped.
- Complete .NET regression suite: 1,072 passed (23 API, 73 website, 976 identity/admin), none failed or skipped.

All four WI-006 acceptance criteria are satisfied. WI-006 is marked done in the authoritative status map. The next incomplete item is WI-007 — Set a unit of measure with step and minimum; it is informational only and has not been started.

The approved Git workflow creates `feature/wi-006-show-category-counts-branch-products` from `main`, commits buildable milestones with WI-006 references, pushes that feature branch to the existing `ColmKenna/Field-Sales` repository, and stops for review before integration.
