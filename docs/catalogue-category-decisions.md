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

## Category model for Increment 3 review

`FieldSales.Api.Catalogue.Category` stores only the stable ID, optional parent ID, and name. `CategoryTree` creates root and child nodes and returns breadcrumb segments with IDs for navigation. It walks parents iteratively, so the domain has no six-level cutoff. It rejects unknown parents and detects broken or cyclic ancestry when reading a breadcrumb.

The implementation trims names and treats sibling names that differ only by case as duplicates (`OrdinalIgnoreCase`). The later SQL store must enforce this rule under concurrent writes; the in-memory check alone is insufficient. Confirm this naming convention in the Increment 3 review.

## Proposed create-page layout for Increment 4 review

- `/HeadOffice/Categories` lists root categories and offers **Add root category** with a name field.
- Opening one category shows its current breadcrumb as links to its ancestors, followed by its direct child categories and an **Add subcategory** name field. The parent is the category being viewed. A successful create opens the new category.
- A duplicate sibling name shows a field error and makes no change. The form uses the existing staff sign-in and Head Office role boundary. There are no product rows, counts, search, move, archive, or recategorise controls in this slice. Rename is added in Increment 5.

## Verification boundary

WI-004 will verify root and nested creation, persistence across restart, a six-level breadcrumb, duplicate leaf names in different branches, role denial, rejected writes, and changed descendant breadcrumbs with stable category IDs after a rename. The developer approved the Human-Led scenario matrix on 2026-09-29.

The source also asks for product breadcrumbs to change while orders and calls remain untouched. None of those record types exists in the application yet. The approved WI-004 evidence is the derived category path and stable category identity; direct assertions over Product, Order, and Call records follow when those models are introduced. This does not authorize adding those models to WI-004.

## Later boundaries

- Counts, search, move, recategorisation, and archive follow their own tasks. The category tree UI in this slice uses the H-15 proposal of one category at a time with its breadcrumb for navigation; the layout remains subject to the Increment 4 review.
- The staff directory is the source of the rep-to-manager reporting line in the story context. Its cardinality and persistence remain a later staff-directory decision. WI-004 records this dependency without adding a reporting relationship to the category model.
- A staff role does not identify a person's manager. The staff directory will supply that reporting line to later work; category requests use the existing Head Office User role and current-role checks.
- Tablet language, Visit Planning administrator permissions, and dated-price day boundaries do not affect this category slice.
