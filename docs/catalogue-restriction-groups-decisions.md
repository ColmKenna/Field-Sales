# Restriction Groups — WI-011

Date: 2026-10-01. Source: T-1.5.3 / PRD-US-008. Status: complete; all acceptance and regression checks passed.

## Approved plan and scope

The developer approved the plan and every recommended decision on 2026-10-01. WI-009 is merged into `main` at `331d5c0` and WI-010 at `5be77e6`. The branch `feature/wi-011-maintain-restriction-groups-archiving-without` was created from `main` at `b4e3efe`. WI-011 is Human Tight-Loop with a Human-Led scenario matrix. Each increment stopped for review, and the developer continued after each one.

Restriction Groups are the fifth list on the shared reference-data screen. They use the same switcher, API routes, BFF client, usage-count interface, retirement policy and transaction protection as Brands, Product Profiles, Attribute names and suppliers. An active unused group offers Delete. An active group in use offers Archive, with its usage. An archived group offers Un-archive. Names follow the existing rules: trimmed, 1–200 characters, and unique within the list without regard to case, including archived names.

This item adds only the product link, the existing-reference display and the storage boundary. The product classification editor is WI-012. Permission records and the real permission count are WI-029. The snapshot builder, tablet hiding and in-progress order lines are WI-043.

## Decisions (developer, 2026-10-01)

| Ref | Question | Decision |
|---|---|---|
| D1 (MI-45) | When a group is archived, are its products visible to every rep, hidden from every rep, or treated as unrestricted? | **Hidden from every rep**, with or without the permission, so the rule fails closed. Products with no group are unaffected. Pharmacy-only medicines cannot become visible by accident. |
| D1b | Should the archive confirmation state the product consequence? | Yes: "N products will be hidden from every rep while archived". |
| D2 | Can a product be assigned to an archived group? | No. A new link is rejected; an unchanged existing link is kept. |
| D3 | Does un-archiving need a confirmation that permissions become effective again? | The existing confirmation step stays and adds "N permissions will take effect again". |
| D4 | Singular wording | "1 permission will be kept but has no effect while archived"; "1 product will be hidden from every rep while archived"; "1 permission will take effect again". |
| D5 | Build the visibility rule now? | Yes, as the single filter that WI-043 applies. |

## Implementation

**Increment 1 — reference list (`e718d97`).**
- `RestrictionGroup : NamedReferenceItem` with its own table, the `restriction-groups` key, and `RestrictionGroupListStore` (required usage source `products`).
- `Products.RestrictionGroupId` gives at most one group per product, with a restrictive foreign key.
- Migration `20261001190518_AddRestrictionGroups` only adds: a table, a column, an index and a foreign key. Existing data is untouched, and EF reports no pending model changes.
- `ProductReferenceUsageSource` counts distinct products per group.
- `Product.SetRestrictionGroup` enforces D2. `ProductReferenceAssignments.SetRestrictionGroupAsync` loads current state in a serializable transaction.
- Product details show the group, labelled `(archived)` when archived.

**Increment 2 — confirmation lines (`d2de0fa`).**
- `ReferenceListDefinition` gained an optional list of `ReferenceActionNote(Action, SourceKey, SingularText, PluralText)`. `NotesFor(action, usage)` returns a line for each note whose source counts more than zero.
- The shared confirmation renders these lines for any list, with no Restriction Group condition in the markup.
- The Restriction Group definition supplies the S5, D1b, D3 and D4 wording.
- Permissions are counted through the existing `IReferenceUsageSource` interface, under the key `RestrictionGroupListStore.PermissionSource` (`"restriction-permissions"`).

**Increment 3 — visibility rule (`1c94b26`).**
- `RestrictedProductVisibility.VisibleToRep(products, groups, permittedGroupIds)` is an `IQueryable<Product>` filter that SQL translates.
- An ungrouped product is visible. A product in an active group is visible only with that group's permission. A product in an archived group is hidden from every rep. A missing group also hides the product.
- It composes with any product query, so the snapshot builder can apply it to suggestions, replacements and pickers alike.

## Obligations for later items

- **WI-029** must register an `IReferenceUsageSource` with key `restriction-permissions` (labels permission/permissions) for the `restriction-groups` list, and add that key to the list's required sources. Until then, production has no permission count, so the S5 line does not appear there. S5 is proven with a test permission source, the same approach WI-009 used for the specialist count. As WI-009 recorded, a separately stored reference needs coordinated protection before Delete may ignore it.
- **WI-043** must filter snapshot products with `VisibleToRep`, passing the rep's current permissions from WI-029, rather than re-deriving visibility. It also owns the T-1.5.3-S question about an In Progress order line (valid when captured). That is not testable here, because orders and the tablet do not exist yet.
- **WI-012**'s classification editor inherits D2 through the storage boundary above.

## Acceptance verification

| Quoted criterion | Result and evidence |
|---|---|
| “Restriction Groups follow Delete-when-unused and Archive-when-used (S1–S4)” | **PASS.** The seven shared `ReferenceListEndToEndTests` theories now include `restriction-groups`. They run through the website, BFF, API and SQL. **S1:** `Should_DeleteOnlyUnusedItems_When_Confirmed` (Delete only, Cancel, removal). **S2:** `Should_PreserveAndLabelExistingReferences_When_UsedItemIsArchived` ("Used by 2 products", Archive only, product labelled `Used (archived)`, excluded from choices, new link rejected, kept through an unrelated price edit). **S3/S4:** `Should_HideArchivedAndRestoreSelection_When_ToggleAndUnarchiveAreUsed` ("Show archived (3)", labelled entries, Un-archive restores selection and new use). |
| “Archiving "High-value equipment", which has 3 reps with permission, shows "3 permissions will be kept but have no effect while archived" (S5)” | **PASS.** `Should_ISee3PermissionsWillBeKeptButHaveNoEffectWhileArchived_When_RestrictionGroupHighValueEquipmentHas3RepsWithPermission` checks that the row reads "Used by 3 permissions" with Archive only, and that the confirmation shows the exact sentence. Confirming archives the group, and its usage still shows 3 permissions. |

## Scenario matrix results

| # | Scenario | Result |
|---|---|---|
| 1 | `Should_SaveAndRenameAcrossRestart_When_ReferenceNameIsValid` (switcher offers five lists) | Pass |
| 2 | `Should_RejectNames_When_InvalidOrDuplicateIncludingArchived` | Pass |
| 3 | `Should_DeleteOnlyUnusedItems_When_Confirmed` (S1) | Pass |
| 4 | `Should_PreserveAndLabelExistingReferences_When_UsedItemIsArchived` (S2, D2) | Pass |
| 5 | `Should_HideArchivedAndRestoreSelection_When_ToggleAndUnarchiveAreUsed` (S3, S4) | Pass |
| 6 | `Should_RefuseDelete_When_ReferenceIsAddedAfterPageLoad` | Pass |
| 7 | `Should_DenyMutationWithoutReplay_When_AccessIsMissing` | Pass |
| 8 | SQL `Should_RefuseDeletion_When_ProductReferencesExist` (SQL error 547) | Pass |
| 9 | S5 exact message | Pass |
| 10 | `Should_ShowPermissionNoteOnlyWhenCounted_When_GroupHas0Or1Permissions` (D4, D1b singular) | Pass (0 and 1) |
| 11 | `Should_SayPermissionsTakeEffectAgain_When_GroupIsUnarchived` (D3, D1b plural) | Pass |
| 12 | `Should_HideProductFromEveryRep_When_ItsGroupIsArchived` (with, without, new rep) | Pass |
| 13 | `Should_ShowProductOnlyToPermittedReps_When_ItsGroupIsActive` | Pass |
| 14 | `Should_ShowProductToEveryRep_When_ItHasNoGroup` | Pass |
| 15 | `Should_RestorePreviousVisibility_When_GroupIsUnarchived` | Pass |

Mutation checks:
- With confirmation-note rendering removed, all four cases of tests 9–11 fail.
- With the archive condition ignored, tests 12, 14 and 15 fail.
- With archived groups treated as unrestricted, tests 12, 14 and 15 fail.

The original code restores green.

## Verification results

- Baseline before changes: 97 API and 201 website tests passed.
- Complete solution build (`dotnet build FieldSales.slnx --no-incremental -warnaserror`): zero warnings and zero errors.
- Complete .NET regression (`dotnet test FieldSales.slnx --no-build --no-restore -m:1 --settings sqlserver.runsettings`): 1,360 passed, with none failed or skipped. That is 102 API, 1,046 identity/admin and 212 website tests. The new total is the baseline plus 1 SQL case and 4 visibility tests (API), and 7 list-theory cases and 4 confirmation cases (website).
- An earlier unbounded `dotnet test FieldSales.slnx` run failed 34 SQL-backed tests before they executed: SQL Server containers crashed at startup ("This program has encountered a fatal error") when every test project started containers at once. No assertion failed. This is the known concurrency limit documented in `docs/refactoring-verification.md`, and the bounded command above is the repository's verification gate.
- JavaScript: all 34 passed (18 identity/admin UI, 11 delivery status, 5 product unit form).
- `git diff --check` is clean, and `dotnet ef migrations has-pending-model-changes` reports no pending changes.

## Changed-file map

- `FieldSales.ReferenceData/ReferenceListKeys.cs`, `ReferenceLists.cs`: list key and count-driven confirmation notes.
- `FieldSales.Api/Catalogue/NamedReferenceItem.cs`, `Product.cs`, `CatalogueDbContext.cs` and the `AddRestrictionGroups` migration/designer/snapshot: entity, product link and storage.
- `ReferenceListStore.cs`, `ReferenceUsageReader.cs`, `ProductReferenceAssignments.cs`, API `Program.cs`: list registration, wording, usage count and storage boundary.
- `RestrictedProductVisibility.cs`: the single visibility rule.
- `ProductEndpoints.cs`, `FieldSales.Catalogue.Contracts/CatalogueResponses.cs`, website `Products/Detail.cshtml`: existing-reference display.
- Website `ReferenceData/Index.cshtml`: confirmation lines.
- `FieldSales.Web.Tests/ReferenceListEndToEndTests.cs`, `FieldSales.Api.Tests/ProductReferencePersistenceTests.cs`, `FieldSales.Api.Tests/RestrictedProductVisibilityTests.cs`: the approved scenarios.
- `plan_docs/.agent-notes/` (WI-011 notes and cross-item decisions), this record, and only the WI-011 entry in `plan_docs/field-sales-delivery/plan-data.js`.

## Resolved information

`{{APPLICATION_REPOSITORY}}` is `https://github.com/ColmKenna/Field-Sales` at `D:/repos/Field-Sales`, and `{{INTEGRATION_BRANCH}}` is `main`. `{{SOURCE_CODE_PATH}}` resolves to `FieldSales.slnx` and the existing catalogue/reference-data code. MI-45 is resolved by D1. No unresolved policy decision remains for this slice. The branch is pushed for review and is not merged; integration remains with the developer.
