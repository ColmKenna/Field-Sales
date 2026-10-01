# Brands and shared reference lists — WI-009 checkpoint

Date: 2026-10-01. Source: T-1.5.1 / PRD-US-008. Status: active; contract design ready for review. Implementation resumes only on `Continue T-1.5.1`.

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

Existing regression suites passed before behaviour changes: 79 API tests and 118 website tests, none failed or skipped. Both test projects built without reported warnings or errors. Source changes have not begun; only this design record and the authorized WI-009 status entry have been added.

## Resolved information and later scope

Repository: `D:/repos/Field-Sales`; integration branch: `main` at `2ac8120`; feature branch: `feature/wi-009-maintain-brands-archive-not-delete`. Existing .NET 10, Razor Pages/BFF, SQL/EF, authorization and test conventions apply. No unresolved policy decision remains after plan approval. Profiles, Attribute names and suppliers belong to WI-010; product assignment UI belongs to WI-011; Restriction Groups and specialist assignment features stay in their own items.
