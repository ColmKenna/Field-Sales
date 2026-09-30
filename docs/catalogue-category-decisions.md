# Category foundation — WI-004

Date: 2026-09-29. Source: T-1.1.1 / Product Management US-005 S4–S5.

## Approved contract

- The first catalogue slice uses the existing .NET 10, Aspire, Razor Pages BFF, Field Sales API, SQL Server, and EF Core stack. The staff website continues to send server-held access tokens to the business API. Head Office User is the only business role that can create or rename categories.
- A category has a stable GUID identity, a name, and an optional parent category identity. The root has no parent. Depth is unlimited; six levels are an explicit display and verification target.
- A breadcrumb is calculated from the current ancestor chain when read. Renaming a node changes the displayed paths of its descendants without changing category identities or rewriting records that refer to those identities.
- Names are unique among siblings. The same leaf name can occur under different parents, where its full breadcrumb disambiguates it.
- A create or rename submission rejected because the staff session expired or the role was removed makes no change. The application does not persist or replay that form submission after sign-in.

## Persistence placement

The existing `StaffWebDb` holds website tickets, protection keys, and last-used area preferences. The established application contract puts business endpoints in `FieldSales.Api`. The developer approved a dedicated SQL Server `CatalogueDb` and EF Core context owned by `FieldSales.Api`, reached from the BFF with its server-held access token, at the Increment 1 review. Category master data stays under the API role boundary and separate from website session storage.

## Category model

`FieldSales.Api.Catalogue.Category` stores only the stable ID, optional parent ID, and name. `CategoryTree` creates root and child nodes and returns breadcrumb segments with IDs for navigation. It walks parents iteratively, so the domain has no six-level cutoff. It rejects unknown parents and detects broken or cyclic ancestry when reading a breadcrumb.

The implementation trims names and treats sibling names that differ only by case as duplicates (`OrdinalIgnoreCase`). `CatalogueDb` has filtered unique indexes for root and child names, using a case-insensitive SQL Server collation to reject concurrent duplicates. Names are limited to 200 characters so both indexes remain bounded.

## Create-page layout implemented in Increment 4

- `/HeadOffice/Categories` lists root categories and offers **Add root category** with a name field.
- Opening one category shows its current breadcrumb as links to its ancestors, followed by its direct child categories and an **Add subcategory** name field. The parent is the category being viewed. A successful create opens the new category.
- A duplicate sibling name shows a field error and makes no change. The form uses the existing staff sign-in and Head Office role boundary. There are no product rows, counts, search, move, archive, or recategorise controls in this slice.

## Rename implemented in Increment 5

The category detail page has a separate **Rename category** form. It sends a server-side token to the Head Office protected API `PUT /catalogue/categories/{id}/name` route. The API changes only the name; IDs and parent links remain fixed. A descendant's next read derives its breadcrumb from the current ancestor names. Duplicate sibling names show a field error. Expired or removed-role form submissions stop before any API write and are not replayed after sign-in.

## Test harness conventions

- Domain rules and breadcrumb derivation: `FieldSales.Api.Tests/CategoryTreeTests.cs` (unit tests).
- Protected API behavior, migration, SQL uniqueness, and restart persistence: `FieldSales.Api.Tests/CataloguePersistenceTests.cs` (SQL Server Testcontainers integration tests).
- Razor Pages, antiforgery forms, and current staff role behavior: `FieldSales.Web.Tests/CategoryPageTests.cs` using the website factory and a test catalogue handler.
- Complete website → API → SQL path: `FieldSales.Web.Tests/CategoryEndToEndTests.cs`, including a host restart.
- Name future story scenario tests `Should_Outcome_When_Condition` so a failing result states the observable rule. Keep the Human-Led approved scenario list as test authority for WI-004.

## Verification boundary

WI-004 verifies root and nested creation, persistence across restart, a six-level breadcrumb, duplicate leaf names in different branches, role denial, rejected writes, and changed descendant breadcrumbs with stable category IDs after a rename. The developer approved the Human-Led scenario matrix on 2026-09-29.

The source also asks for product breadcrumbs to change while orders and calls remain untouched. None of those record types exists in the application yet. The approved WI-004 evidence is the derived category path and stable category identity; direct assertions over Product, Order, and Call records follow when those models are introduced. This does not authorize adding those models to WI-004.

## Later boundaries

- Counts, search, move, recategorisation, and archive follow their own tasks. The category tree UI in this slice uses the reviewed H-15 proposal of one category at a time with its breadcrumb for navigation.
- The staff directory is the source of the rep-to-manager reporting line in the story context. Its cardinality and persistence remain a later staff-directory decision. WI-004 records this dependency without adding a reporting relationship to the category model.
- A staff role does not identify a person's manager. The staff directory will supply that reporting line to later work; category requests use the existing Head Office User role and current-role checks.
- Tablet language, Visit Planning administrator permissions, and dated-price day boundaries do not affect this category slice.
