# Brands and shared reference lists — WI-009 checkpoint

Date: 2026-10-01. Source: T-1.5.1 / PRD-US-008. Status: complete; all acceptance and regression checks passed.

The developer reviewed the checkpoint in commit `a03d16d` and resumed with `Continue T-1.5.1`. The sections below retain the reviewed design; implementation and verification evidence follow them.

## Approved scope and policies

The developer approved Brands creation/rename, unused deletion, used archiving, archived visibility and restoration. Names are trimmed, required, at most 200 characters and case-insensitively unique across active and archived Brands. The minimum primary/alternative Brand relationship storage is included for counts and existing-record labels; the product assignment editor remains WI-011. Specialist assignment features are not implemented here. A test provider supplies the specialist count in the source scenario; production never invents references.

The single reference-data screen initially has Brands in its list switcher. Only Delete or Archive is offered on an active item, determined by its usage. An archived item offers Un-archive. The item rule takes precedence over the older shared UX sketch showing both Delete and Archive for unused items.

The approved scenario matrix is recorded below. No new test scenarios or production features are introduced by this design checkpoint.

## Reference-count interface signature

These are proposed signatures, not yet installed production code. The common contracts will live in a small dependency-free `FieldSales.ReferenceData` library shared by API and website.

```csharp
public readonly record struct ReferenceItemKey(string ListKey, Guid ItemId);
// ListKey is a registered key, initially "brands". It is not a type name from a request.

public sealed record ReferenceCount(string SourceKey, string SingularLabel,
    string PluralLabel, long Count);

public interface IReferenceUsageSource
{
    string SourceKey { get; } // Stable and unique within the registry.
    bool Supports(string listKey);
    Task<ReferenceCount> CountAsync(ReferenceItemKey item,
        CancellationToken cancellationToken);
}

public interface IReferenceUsageReader
{
    Task<ReferenceUsage> ReadAsync(ReferenceItemKey item,
        CancellationToken cancellationToken);
}

public sealed class ReferenceUsage
{
    public IReadOnlyList<ReferenceCount> Counts { get; }
    public bool IsUsed { get; }
    public string Description { get; } // "Used by 24 products and 1 specialist assignment"
    // Constructor validates source uniqueness, labels and nonnegative counts;
    // copies inputs and omits zero entries from Description. Empty: "Not used yet".
}

public enum ReferenceAction { Delete, Archive, Unarchive }
public static class ReferenceRetirementPolicy
{
    public static ReferenceAction Decide(bool isArchived, ReferenceUsage usage);
}
```

`ReferenceUsageReader` combines every registered applicable source in stable registration order. An unknown list key, missing required source, duplicate source key, malformed result or failed lookup is an error, never an empty count. Required source keys are supplied by the registered list definition. The API then returns an unavailable response and performs no retirement operation. The same policy selects both the displayed action and the server-side permitted action. An active item with no references resolves to Delete; one in use resolves to Archive; an archived item resolves to Unarchive.

Each source counts distinct records in its own area. Product membership means primary OR alternative, with a product counted once even if both links exist. Different source labels represent separate record types and are kept separately rather than treated as a misleading total.

## Generic list component contract

```csharp
public sealed record ReferenceListDefinition(string Key, string SingularLabel,
    string PluralLabel, IReadOnlyList<string> RequiredUsageSources);

public sealed record ReferenceListItem(Guid Id, string Name, bool IsArchived,
    ReferenceUsage Usage);

public sealed record ReferenceListViewModel(ReferenceListDefinition SelectedList,
    IReadOnlyList<ReferenceListDefinition> AvailableLists,
    IReadOnlyList<ReferenceListItem> Items, int ArchivedCount, bool ShowArchived);
```

A shared Razor partial under `Pages/Shared/` consumes the view model. It renders the list switcher, add/rename forms, archived toggle, usage sentence, archived labels and the action selected by `ReferenceRetirementPolicy`. Confirmation is a server-rendered step with Cancel; archive confirmation explains: "It stays on existing records and stops appearing in new selections." Ordinary add/rename saves need no confirmation. Forms use the existing antiforgery and Head Office authorization conventions.

One `ReferenceData/Index` PageModel and BFF client route by a registered list key. A list definition controls labels and required usage sources; an API handler owns that list's persistence and validation. User-supplied list keys cannot select arbitrary entities. Brands is the sole registered list in this item.

WI-010 will register definitions/providers/handlers for Profiles, Attribute names and suppliers, using this same page and partial. It will not copy the Brand page or retirement policy. New list-specific behaviour must stay in its handler, not in conditional Brand markup.

The website uses the usage and state returned by the protected API. It has no direct database access to business references. The API rechecks current usage and state on confirmation; hidden controls are not security enforcement.

## Worked example: Brand used by products

The production `ProductBrandUsageSource` will use the shared scoped `CatalogueDbContext`. Brand storage, the optional primary link and alternative links will be added after checkpoint review, with restrictive foreign keys. The query counts distinct product IDs across primary and alternative membership.

```csharp
// Proposed production adapter; implementation follows the approved checkpoint.
public sealed class ProductBrandUsageSource(CatalogueDbContext db)
    : IReferenceUsageSource
{
    public string SourceKey => "products";
    public bool Supports(string listKey) => listKey == "brands";
    public Task<ReferenceCount> CountAsync(ReferenceItemKey item,
        CancellationToken cancellationToken);
}

// Worked scenario values returned by adapters:
var productCount = new ReferenceCount("products", "product", "products", 24);
var specialistCount = new ReferenceCount("specialist-assignments",
    "specialist assignment", "specialist assignments", 1);
// Reader => IsUsed: true
// Description: "Used by 24 products and 1 specialist assignment"
// Policy for active SunCo => Archive; no Delete control.
```

The specialist value is supplied only by a test implementation until that area exists. With only the production product source registered, the same Brand reports "Used by 24 products". Alternative-only membership also prevents deletion. Archiving SunCo keeps its links and shows `SunCo (archived)` on its products. Active-selection queries exclude it; restoring it includes it again.

## Delete consistency and future-provider obligation

For Brands, the API opens a serializable SQL transaction, loads the current Brand state and reads usage through the scoped reader inside that transaction before deleting. The product adapter shares that transaction through the same scoped context. Restrictive foreign keys protect both primary and alternative links; a concurrent reference cannot be silently cascaded away. A raced or stale Delete returns a conflict and leaves the Brand intact. Archive and restore also recheck current state. New Brand links must validate active state in a transaction; unchanged existing archived links may be preserved.

The count interface is extensible, but a remote count alone cannot make deletion atomic across independent databases. Future areas must join the coordinated transaction or maintain durable local reference protection before their retirement support is enabled. Registering a source is mandatory when an area starts creating references. A registry requirement makes a missing provider fail closed; it cannot discover an unregistered future area automatically. The checkpoint review should assess this explicit obligation rather than assume an interface alone prevents every future integration mistake.

## Approved scenario matrix

| Test name | Intent |
|---|---|
| Should_SaveBrand_When_NameIsValid | Create and rename persist after restart. |
| Should_RejectName_When_BlankTooLongOrDuplicate | Invalid names and duplicates, including archived names, leave records unchanged. |
| Should_DeleteBrand_When_UnreferencedAndConfirmed | Delete is offered; confirmation removes it and cancellation preserves it. |
| Should_OfferArchive_When_ReferencesExist | Delete is hidden; combined product/specialist usage is accurate. |
| Should_PreserveProductReferences_When_BrandIsArchived | Existing primary/alternative references remain labelled (archived). |
| Should_ExcludeArchivedBrand_When_NewSelectionsAreRequested | Archived Brands cannot be newly selected. |
| Should_HideArchivedBrands_When_ToggleIsOff | Archived items are hidden by default with the correct Show archived (N) count. |
| Should_RestoreSelection_When_BrandIsUnarchived | Restored Brands are selectable again. |
| Should_CountProductOnce_When_BrandHasMultipleLinks | Primary/alternative membership counts each product once. |
| Should_RefuseDelete_When_ReferenceAppearsAfterPageLoad | Stale confirmation and concurrent reference creation cannot break links. |
| Should_PreserveArchivedBrand_When_ExistingProductIsEdited | An unrelated edit retains existing archived references. |
| Should_DenyMutation_When_StaffAccessIsMissing | Website/API reject unauthorized writes and expired sessions. |

## Baseline verification

Existing regression suites passed before behaviour changes: 79 API tests and 118 website tests, none failed or skipped. Both test projects built without reported warnings or errors. At the design checkpoint, source changes had not begun; only this design record and the authorized WI-009 status entry had been added.

## Resolved information and later scope

Repository: `D:/repos/Field-Sales`; integration branch: `main` at `2ac8120`; feature branch: `feature/wi-009-maintain-brands-archive-not-delete`. Existing .NET 10, Razor Pages/BFF, SQL/EF, authorization and test conventions apply. No unresolved policy decision remains after plan approval. Profiles, Attribute names and suppliers belong to WI-010; product assignment UI belongs to WI-011; Restriction Groups and specialist assignment features stay in their own items.

## Implemented contracts and reusable list

The signatures above are implemented in `FieldSales.ReferenceData/ReferenceLists.cs`. The immutable usage snapshot copies and validates counts. `ReferenceRetirementPolicy` is the sole place selecting Delete, Archive or Unarchive. `ReferenceUsageReader` checks the registered list and required providers, evaluates all applicable providers sequentially through the shared scoped context, and refuses malformed, missing or failed usage checks. Neither a read nor a retirement request silently substitutes zero for unavailable usage.

`IReferenceListStore` in `FieldSales.Api/Catalogue/ReferenceListStore.cs` is the API extension point: a list definition plus list/find/name-save/retirement methods. `BrandListStore` is the first implementation. `ReferenceListEndpoints` serves every registered list through the same protected routes; it does not select arbitrary EF entities from a request. `ReferenceDataApiClient`, the one `ReferenceData/Index` PageModel and `_ReferenceList.cshtml` serve the same registered definitions on the website.

For WI-010, a new store/provider registration will look like this (illustrative future types, not current registrations):

```csharp
// A store supplies its own entity and persistence operations:
public ReferenceListDefinition Definition { get; } =
    new("profiles", "Product Profile", "Product Profiles", ["products"]);

// Register the store and its transaction-safe usage source:
services.AddScoped<IReferenceListStore, ProductProfileListStore>();
services.AddScoped<IReferenceUsageSource, ProductProfileUsageSource>();
```

The existing switcher takes `AvailableLists` from registered stores. Selecting `profiles` would use the same API handlers, BFF methods, page, partial, usage rendering, confirmation flow and policy. No list page or retirement decision is copied. That registration is intentionally not implemented by WI-009.

Migration `20261001085456_AddBrandsAndProductReferences` adds Brands with a case-insensitive unique name and rowversion, optional `Products.PrimaryBrandId`, alternative link storage, indexes and restrictive foreign keys. Existing products have no Brand links after upgrade. Price history, category identity, attributes and quantity rules remain intact.

`ProductBrandUsageSource` counts distinct products whose primary or alternative link matches the Brand. `ProductBrandAssignments` is a storage boundary for future consumers, not a new product assignment editor or HTTP endpoint: it loads Brand state and existing links in a serializable transaction, rejects new archived references and allows unchanged archived references. Product details expose Brand labels and primary/alternative roles. No new Brand picker is added to product creation; the protected active-choice contract is ready for WI-011.

Retirement reads usage in its serializable transaction and applies the shared policy to current state, not the action shown by an earlier page load. SQL restricts deletion through either reference type, even if a caller bypasses the count interface. Rowversion detects competing mutations. A changed action, reference conflict or deadlock is reported as a conflict without cascading references. The source checkpoint's future-provider obligation remains: independently stored references need durable coordinated protection before deletion is enabled for those areas.

## Acceptance criteria — pass/fail and evidence

| Quoted criterion | Result and evidence |
|---|---|
| “A Brand with no references shows Delete (not Archive); confirming removes it.” | **PASS.** `Should_DeleteBrand_When_UnreferencedAndConfirmed` checks the rendered row, confirmation/Cancel, persisted removal and subsequent 404. |
| “A Brand used by 24 products and 1 specialist assignment shows Archive (not Delete) with ‘Used by 24 products and 1 specialist assignment’.” | **PASS.** `Should_OfferArchive_When_ReferencesExist` seeds 24 real SQL product references and registers the reviewed test specialist provider; verifies the exact sentence, absence of Delete, archive explanation, saved archive state and retained products. No production specialist count is fabricated. |
| “An archived Brand stays on its products labelled ‘(archived)’ and is not offered for new selections.” | **PASS.** `Should_PreserveProductReferences_When_BrandIsArchived` covers both primary and alternative links through the website/API. `Should_ExcludeArchivedBrand_When_NewSelectionsAreRequested` verifies the active-choice contract and rejected new assignment. `Should_PreserveArchivedBrand_When_ExistingProductIsEdited` preserves links through an unrelated price edit. |
| “The list hides archived items behind ‘Show archived (N)’.” | **PASS.** `Should_HideArchivedBrands_When_ToggleIsOff` checks three archived Brands, default hiding and all three labelled entries after enabling the toggle. |
| “Un-archiving makes the Brand selectable again.” | **PASS.** `Should_RestoreSelection_When_BrandIsUnarchived` restores through website confirmation and verifies the active-choice result. |
| “Usage counts are gathered through one interface that any area can implement (‘what references item X?’); products implement it now.” | **PASS.** The shared `IReferenceUsageSource` / `IReferenceUsageReader` contracts are used by the real product source and the test specialist source. `Should_CountProductOnce_When_BrandHasMultipleLinks` covers combined/alternative membership. Missing and failed providers return 503 and cannot retire a Brand. |

Additional required self-verification:

- **PASS — Delete refused when a reference appears between page load and confirm.** The stale confirmation case returns the conflict message and Archive instead. Its concurrent variant holds a real SQL product-reference transaction open, waits for the deletion reader to reach the count operation, then commits the reference; deletion returns 409 and preserves the link. Separate SQL tests prove both primary and alternative foreign keys reject direct deletion with SQL error 547.
- **PASS — List component reusable without copying.** The shared page/partial and registry-driven API/BFF are used for Brands; the WI-010 registration example above shows the extension point.
- **PASS — Commit messages reconciled with actual diff.** The contract milestone, protected persistence/API and reusable website flow have separate scoped commits, with the required WI/task/story references and no attribution trailers.

## Verification results and file map

- Baseline before changes: 79 API and 118 website tests passed.
- Focused Brand flow: 23 cases passed. Final suite additionally includes rename validation and missing-antiforgery checks within those scenarios.
- Focused SQL: 3 cases passed, covering predecessor upgrade, archived-name uniqueness and restrictive links of both types.
- Final build: zero warnings/errors.
- JavaScript: all 34 passed (18 identity/admin UI, 11 delivery status, 5 product unit form).
- Complete .NET regression: 1,199 passed (82 API, 141 website, 976 identity/admin), none failed or skipped.

Changed files:

- `FieldSales.ReferenceData/`, solution and project references — shared contracts and retirement policy.
- `FieldSales.Api/Catalogue/Brand.cs`, `Product.cs`, `CatalogueDbContext.cs` and Brand migration files — Brand/reference storage and guards.
- `ReferenceUsageReader.cs`, `ReferenceListStore.cs`, `ReferenceListEndpoints.cs` and API `Program.cs` — providers, list registry, transaction boundary and protected operations.
- API product endpoint and website catalogue client/product Detail — archived existing-reference labels.
- `FieldSales.Web/Catalogue/ReferenceDataApiClient.cs`, `Pages/HeadOffice/ReferenceData/`, `Pages/Shared/_ReferenceList.cshtml`, Head Office index and site CSS — shared reference-list experience.
- `FieldSales.Api.Tests/BrandPersistenceTests.cs`, `FieldSales.Web.Tests/BrandEndToEndTests.cs` — the reviewed scenarios and SQL verification.
- This evidence record and only the authorized WI-009 status entry in `plan-data.js` — reviewed decisions and delivery state.

The authorized status map marks WI-009 done. No console card content was changed. The completed feature branch is pushed for developer review; it is not merged. WI-010 is next in console order, informational only, and has not been started.
