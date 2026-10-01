# Product reference lists — WI-010

Date: 2026-10-01. Source: T-1.5.2 / PRD-US-008. Status: active; implementation and focused verification complete, full regression pending.

## Approved plan and scope

The developer approved extending the reviewed Brand pattern to Product Profiles, Attribute names and suppliers. WI-009 is merged into main at `331d5c0`. WI-010 is autonomous, with no intermediate checkpoint. The feature branch is `feature/wi-010-apply-archive-not-delete-product`.

All four lists use the same screen, registered API routes, BFF client, reference-count contracts, retirement policy and transaction protection. An active unused item offers Delete; an active referenced item offers Archive with usage; an archived item offers Un-archive. Archive confirmation explains that existing references stay and new selections stop offering the item.

Name rules carry forward from the approved Brand policy: trim whitespace, require 1–200 characters, enforce case-insensitive uniqueness within each list including archived entries. The same name may occur in different lists. Product Profiles and suppliers are optional single product references. Attribute names are global and have stable identities; their product values are retained separately.

This item supplies the minimum relationship storage and existing-reference display needed for retirement. The product classification editor is WI-012, attribute entry and tablet delivery are WI-033, Restriction Groups are WI-011, and Location/Contact Types are WI-017. No product assignment HTTP endpoint or editor has been added.

## Shared implementation

`NamedReferenceItem` centralizes name validation, identity, archive state and rowversion. Brand, ProductProfile, AttributeName and Supplier have distinct tables and small factories; EF explicitly maps them as separate roots. The migration does not rewrite the Brand table.

`ReferenceListStore<T>` contains list/find/name-save and serializable retirement operations once. `BrandListStore` now uses that implementation, and the three new store registrations provide only entity, labels and list key. The unchanged `ReferenceRetirementPolicy` determines both rendered controls and permitted actions. The unchanged `ReferenceListEndpoints`, website reference-data PageModel, BFF methods and `_ReferenceList` partial serve all four registrations without copied pages or list logic.

`ProductReferenceUsageSource` supplies the reviewed interface for profiles, attribute names and suppliers. Each counts distinct referencing products. Multiple attribute values with the same name on a product count that product once. Existing Brand and optional future-area providers continue through the same reader. Required-source and failed-source checks still prevent a usage lookup from silently becoming zero.

`ProductReferenceAssignments` is the transaction-safe storage boundary for future product editors. It loads current referenced items and product state inside a serializable transaction, rejects new archived links, preserves unchanged archived classification links and adds attribute values only for active names. Foreign keys restrict deletion through all three relationship types. No cascading reference removal is enabled.

The product-detail API loads the referenced profile, supplier and attribute names. The website labels each archived reference `(archived)` and keeps attribute values displayed. Optional DTO fields preserve the predecessor's existing website test/client constructions. Unrelated unit and price saves do not rewrite reference storage.

## Attribute migration and preservation

Migration `20261001100454_AddProductReferenceLists` adds the three reference tables, optional product profile/supplier keys and `ProductAttributeValues` with stable name foreign keys and ordinal positions. The generated migration was amended to copy legacy JSON attributes before dropping the replaced column.

Upgrade validates the legacy array and name/value rows, creates case-insensitively unique trimmed global names, copies every value and original row position, verifies matching row counts and then removes the legacy JSON column. Repeated values are retained as separate rows; usage counts distinct products rather than rows. Empty, quoted, newline, Unicode and long values are preserved. Invalid input or unsupported names abort the migration transaction rather than truncate or discard data. Existing products, categories, prices, Brand links and quantity rules remain intact.

Rollback reconstructs ordered name/value JSON before removing the new relationship tables. It uses the current attribute names, so a renamed attribute does not revert to its former label. Application-assigned attribute-value IDs are explicitly mapped with `ValueGeneratedNever` to ensure newly attached values are inserted.

## Acceptance verification

| Quoted criterion | Result and evidence |
|---|---|
| “For each of Product Profiles, Attribute names and suppliers: an unused item offers Delete only; a used item offers Archive only, with its usage.” | **PASS.** `Should_DeleteOnlyUnusedItems_When_Confirmed` and `Should_PreserveAndLabelExistingReferences_When_UsedItemIsArchived` run for all three lists through website, API and SQL. They verify controls, Cancel, removal and exact product usage counts. |
| “Archived items stay on existing products labelled ‘(archived)’ and are not offered for new selections.” | **PASS.** The archive scenario checks product labels, retained values, active-choice exclusion, rejected new assignment and preservation after an unrelated website price edit for every list. Existing archived classification links can remain unchanged. |
| “Each list hides archived items behind ‘Show archived (N)’; un-archive restores selection.” | **PASS.** `Should_HideArchivedAndRestoreSelection_When_ToggleAndUnarchiveAreUsed` checks three hidden archived entries, labelled visible entries, restoration, choice inclusion, successful new use and the updated count for every list. |
| “The H-17 list switcher offers all four lists.” | **PASS.** `Should_SaveAndRenameAcrossRestart_When_ReferenceNameIsValid` checks Brands, Product Profiles, Attribute names and suppliers in the rendered switcher for every new list and verifies create/rename persistence after API/website restart. |

Further evidence:

- `Should_RejectNames_When_InvalidOrDuplicateIncludingArchived`: blank/oversized names and case/whitespace duplicates cannot create or rename an item; archived names remain reserved; separate lists may share a name.
- `Should_PreserveValuesAndUsage_When_AttributeNameIsRenamed`: two values on one product retain their order and values after rename and restart; the usage remains one product.
- `Should_RefuseDelete_When_ReferenceIsAddedAfterPageLoad`: stale confirmation and direct deletion are refused for all three lists. `Should_RefuseDeletion_When_ProductReferencesExist` proves each SQL foreign key rejects deletion with error 547.
- `Should_DenyMutationWithoutReplay_When_AccessIsMissing`: Head Office role removal, rep/manager access and expired sessions cannot mutate any of the new lists through website or API; signing back in does not replay the attempted operation.
- `Should_PreserveLegacyAttributesAndProductData_When_ReferenceMigrationUpgradesAndRollsBack`: upgrades a real predecessor database containing repeated attributes, an empty value, quoted/newline/Unicode text and a value over 6,000 characters; verifies values/order, distinct names, product data, rename, rollback and upgrade again.
- `Should_KeepLegacyData_When_MigrationFindsInvalidAttributeInput`: invalid arrays/rows/names/values leave original data and migration state unchanged.

## Verification results

- Before behaviour changes: all 82 API and 141 website tests passed.
- Focused new website scenarios: 22 passed, none failed or skipped.
- Focused SQL scenarios: 5 passed, none failed or skipped.
- Complete solution build: zero warnings/errors.
- JavaScript: all 34 passed (18 identity/admin UI, 11 delivery status, 5 product unit form).
- Full .NET regression: pending.

Required self-verification: all four criteria demonstrated for all three lists; shared list logic and policy reused without copying; provisional commit messages reconciled with the final diff. Existing Brand scenarios, including concurrent-reference deletion and unavailable usage providers, remain in the full regression suite.

## Changed-file map and resolved information

- `FieldSales.Api/Catalogue/NamedReferenceItem.cs`, `Brand.cs`, `ReferenceListStore.cs` — shared item behaviour and four store registrations.
- `Product.cs`, `ProductReferenceAssignments.cs`, `ReferenceUsageReader.cs`, `CatalogueDbContext.cs`, migration/designer/snapshot — stable relationship storage, usage and migration.
- API `Program.cs` — registration of three lists, product usage source and storage boundary.
- API `ProductEndpoints.cs`, website `CatalogueApiClient.cs` and product `Detail.cshtml` — existing-reference projections and archive labels.
- `FieldSales.Api.Tests/ProductReferencePersistenceTests.cs`, `FieldSales.Web.Tests/ReferenceListEndToEndTests.cs` — selected meaningful scenarios.
- This record and only the WI-010 status entry in `plan_docs/field-sales-delivery/plan-data.js` — decisions, evidence and progress.

Repository, stack and source paths resolve to the existing .NET 10/Razor Pages/BFF/SQL implementation in `D:/repos/Field-Sales`, integration branch `main`. There are no unresolved placeholders or policy decisions. Only the approved slice is implemented; the branch is pushed for review when the full suite is green, and integration remains with the developer.
