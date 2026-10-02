# WI-015 — Geography archive and reference contracts

The user approved the plan and scenario list on 2026-10-02. Source task
T-2.1.2 / DIR-US-007 S4. Autonomous; no intermediate approval checkpoint.

## Approved behaviour

- Active unused places offer Delete; active used places offer Archive with
  usage; archived places offer Un-archive. The existing shared
  `ReferenceRetirementPolicy` makes this decision for both domains.
- County usage counts distinct Town records and Region usage counts distinct
  County records, including archived children. Counts use batch SQL reads.
- Archive affects only the selected row. Its descendants and existing links
  stay intact. Town choices require an active Town, County and Region.
- Archived entries are hidden by default; Show archived includes them with
  labels. Names remain unique among siblings including archived entries.
- CSV preserves existing archival state. Adding beneath an archived ancestor
  rejects the complete import without partial changes.
- Directory count providers are keyed `directory`, so Catalogue registrations
  and database dependencies remain separate. All providers use the existing
  `IReferenceUsageSource` and `ReferenceUsage` contracts.
- Both geography and catalogue screens use `_ReferenceRetirement` and
  `_ReferenceConfirmation`. Hierarchy navigation and geography renaming stay
  on the geography page. Existing catalogue confirmation wording stays intact.

## HTTP and transactional boundary

All routes remain beneath `/directory/geography` with Head Office role and
staff-scope authorization. Browser requests use the Razor Pages BFF; tokens
stay server-side and retirement POSTs require antiforgery.

`GET /?regionId=...&countyId=...&showArchived=true` returns usage, archive
state and archived count. `GET /{level}/{id}` reads an individual usage
snapshot. `POST /{level}/{id}/retire` accepts `Action` and base64 `Version`,
returning a saved result, 400 for invalid requests, 404 for absent records,
409 for changed state/usage and 503 for unavailable or malformed usage.

Creation and retirement run in serializable DirectoryDb transactions. The
current archive state, rowversion and reference counts are checked before a
write. Restrictive parent foreign keys remain in place. Missing, duplicate,
incomplete, negative or failed usage sources never silently become zero.

`GET /town-choices` excludes any archived part of a path.
`GET /towns/{id}/reference` retains the same Town/County/Region IDs and labels
each archived component for existing records. `TownChoice.IsSelectable`
expresses whether the entire path is active.

The additive `20261002102647_AddGeographyArchive` migration adds three
non-null flags defaulting to false; it preserves predecessor records.

## WI-016 connection required

No production Location records exist yet. `EmptyLocationUsageSource` is the
explicit pre-Location provider, required under source key `locations`. The
four-Location acceptance fixture replaces it with a test provider containing
four distinct referencing records. This does not fabricate production usage.

When WI-016 introduces Location storage it must:

1. Replace the keyed `EmptyLocationUsageSource` registration with a real
   source named `locations`, counting distinct records via the same scoped
   DirectoryDbContext. Missing/duplicate registrations must continue to fail.
2. Call `GeographyStore.TownForAssignmentAsync` inside the same serializable
   transaction as a Location save. Pass the stored current Town ID, never a
   browser-supplied claim of an existing reference. New links require an
   active complete path; an unchanged existing archived link is retained.
3. Use the restrictive Location-to-Town foreign key and existing-reference
   projection for labels. Verify those real SQL references and the Location
   forms in WI-016. This screen-level evidence is outside WI-015's approved
   slice.

## Acceptance evidence

| Source acceptance criterion | Evidence |
| --- | --- |
| “Laragh with 4 Locations shows Archive with \"Used by 4 locations\" and no Delete; an unused Town shows Delete only.” | `Should_OfferArchiveWithFourLocations_When_LaraghIsUsed` checks the exact row and confirmation text through website/BFF/API/SQL using the approved four-reference provider. `Should_OfferOnlyDelete_When_PlaceIsUnused` covers Towns, Counties and Regions, confirms cancellation and then actual deletion. |
| “An archived Town is not offered on new Locations; existing Locations keep it, labelled \"(archived)\".” | The Laragh scenario keeps all four fixture references, stable hierarchy IDs and `Laragh (archived) — Wicklow, Leinster`; checks active-choice exclusion, unchanged assignment acceptance, new assignment rejection and restoration. Actual Location forms/foreign-key storage remain the explicitly approved WI-016 connection. |
| “The same rule applies to Counties (used by Towns) and Regions (used by Counties).” | `Should_PreserveDescendantsAndFilterChoices_When_ParentIsArchived` checks real SQL child counts, Archive only, rejected used deletion, retained records and flags, archived labels, ancestor choice exclusion, denied child creation and restoration. `Should_CountArchivedChildrenAndKeepChoicesHidden_When_OnlyChildIsRestored` checks archived child usage and parent precedence. |

Additional integration checks cover repeated imports without reactivation,
atomic rollback, archived-name uniqueness, stale state, changed usage,
invalid action/version, missing rows, malformed/unavailable count providers,
authorization, role removal, antiforgery, restart persistence, predecessor
upgrade and concurrent child creation versus retirement.

Before changes, all 18 existing geography scenarios passed. The focused
geography/shared-reference run passed all 75 tests (18 predecessor geography,
24 new retirement cases and 33 shared-reference cases) with no skips; all 34
JavaScript checks passed. The final solution build passed with zero warnings
or errors. The complete bounded SQL Server suite passed all **1,469 tests**:
143 API, 1,046 Identity admin and 280 website, with zero failures or skips.
TRX evidence is under `.artifacts/wi015-before`, `.artifacts/wi015-focused`
and `.artifacts/wi015-full` (local ignored artifacts).

WI-015 is marked done in the authorized status map; no card content changed.
The three scoped commits complete domain/contracts, shared UI, then verification
and handover. Feature branch: `feature/wi-015-archive-town-that-is-use`.
Integration is separate. WI-016 — Create a customer with its locations — is next;
it has not been started.

No moving/territory/Location forms are added.
