# WI-017 — Maintain Location Type and Contact Type lists

Source T-2.2.1 / DIR-US-009 and DIR-US-001. The user approved the complete
autonomous plan and scenario matrix on 2026-10-02. Starting main: `39c3075`.

## Delivered behaviour

Head Office → Reference data offers Location Types and Contact Types in the
same switcher as the five catalogue lists. Head Office can add and edit a name
and optional description. Names are trimmed, required, limited to 200 characters
and unique without case differences within their own list, including archived
names. The lists may have the same name independently. Descriptions are trimmed,
limited to 2,000 characters and support multiple lines; blank becomes null.
Descriptions explain a Type and do not implement stock or visit policy.

Used Types offer Archive; unused Types offer Delete. Both actions require the
existing plain confirmation and permit cancellation. Show archived reveals
archived entries and Un-archive restores them. Stored references retain their
stable IDs and display `(archived)` after the Type name.

Type is optional on the Customer's first Location, additional Locations and
Location edits. New assignments accept active Location Types only. An existing
archived Type can survive an unrelated edit, or be cleared. Location records
and the Customer's Location table display the Type. Customer creation still
requires a first Location and Town, saved together.

## Persistence and access

`20261002122059_AddDirectoryTypes` adds independent LocationTypes/ContactTypes
tables, descriptions, rowversions and unique name indexes. The nullable
LocationTypeId column has a restrictive foreign key and an index. Existing
Locations retain their ownership, geography, Eircode and IDs, with no guessed
classification or seeded Types. The predecessor upgrade is checked with real
Customer, Location and archived Town data.

Type saves/retirement and Location assignments use the same serializable
DirectoryDb transaction pattern. Location usage counts actual persisted rows,
including links to archived Types. A stale version or changed usage after
confirmation saves nothing. Invalid/missing assignments cannot leave a Customer
without its required first Location. Restrictive SQL constraints and concurrent
assignment/retirement checks preserve references.

Keyed Directory stores and usage sources reuse the reference-list interfaces,
usage validation, archive policy and shared Razor partials. They do not resolve
catalogue usage against DirectoryDb. Optional shared description/version fields
are omitted from old catalogue responses when null. All seven switcher choices
are verified through the existing website.

The API requires current Head Office access and staff API scope. Razor Pages
retain server-held bearer tokens, current-role checks and antiforgery. Missing,
duplicate, unavailable or malformed required usage providers fail closed.
Invalid input returns 400, missing records 404, stale/change conflicts 409 and
unavailable usage 503. Rejected forms retain submitted input.

API root: `/directory/reference-data/{listKey}`, where listKey is
`location-types` or `contact-types`. GET lists, POST creates; GET/PUT `/{id}`
read/update; POST `/{id}/retire` requires Action and Version. GET `/choices`
returns active choices, while GET `/{id}/reference` returns a linked Type with
its archive label. Location create/edit contracts accept optional LocationTypeId.

## Contact boundary and WI-018 handover

Contact records and screens are WI-018. WI-017's production contacts usage
provider deliberately reports zero because no Contact table exists yet.
The approved 14-contact scenario uses a fixture containing 14 distinct Contact
IDs linked to Buyer, including inactive examples. It verifies the exact usage,
Archive-only action, unchanged links and archived-labelled Type projection.
This is fixture evidence; production Contact storage and screens are not claimed.

When implementing WI-018:

- Replace `EmptyContactTypeUsageSource` under `directory-types` with a scoped
  source counting all persisted referencing Contacts, including Inactive ones.
  Its single and batched reads must use the same DirectoryDb context and
  retirement transaction. Do not register it alongside the empty source.
- Add Contact-to-Type restrictive relationships, active-only new assignments
  and retention of unchanged archived references. Consume the existing
  DirectoryTypeChoice.Label/archive projection on Contact screens.
- Repeat the Buyer/14-contact test against real SQL Contacts and their pages;
  retain the usage failure checks and retirement/assignment race coverage.

Location Type is separate from future Location Profile/master work. No Contact
creation, coordinates, profiles, stock rules or visit scheduling is included.

## Acceptance criteria and evidence

The source criteria are quoted below. Evidence is in
`FieldSales.Web.Tests/DirectoryTypeEndToEndTests.cs`.

| Source criterion | Verification |
| --- | --- |
| “Adding Location Type \"Head office — no stock held\" with a description offers it on Locations.” | `Should_OfferDescribeAndSaveType_OnFirstAdditionalAndEditedLocations` submits the shared list form, verifies the trimmed description, uses the choice on first/additional Location forms, reads the saved Type and Customer table, and clears it through the edit form. |
| “Contact Type \"Buyer\" used by 14 Contacts shows Archive (not Delete) and stays on the 14 labelled \"(archived)\".” | `Should_ArchiveBuyerWithFourteenDistinctContactReferencesAndKeepTheirLabels` checks exactly 14 fixture records (including inactive ones), `Used by 14 contacts`, Archive only, cancellation, confirmed archive and all unchanged IDs/type links with `Buyer (archived)` from the reference projection. Subject to the explicit pre-Contact limitation above. |
| “An unused Type shows Delete with a plain confirmation.” | `Should_DeleteUnusedTypeOnlyAfterPlainConfirmation` checks both lists, Delete, named confirmation and Cancel, then confirms deletion. Used Location Type deletion and changed usage after confirmation are rejected. |
| “A Location's Type is optional and set from the active Location Types.” | First/additional/edit scenarios verify optional selection and clearing; archive tests reject new assignments while retaining existing labelled links. The predecessor migration preserves an untyped Location; existing Customer tests continue to create untyped Locations successfully. |
| “Both lists appear in the H-17 list switcher.” | `Should_OfferAllSevenListsFromEverySwitcherWithoutChangingCatalogueResponses` opens each of the seven lists, checks all switcher entries and verifies separate API registries. Existing shared-reference tests remain unchanged. |

Additional scenarios cover both lists' validation/limits/uniqueness, input
retention, restart persistence, no-op and stale versions, missing records,
untrusted usage, actual Location counts, Un-archive, atomic invalid first-Type
creation, SQL foreign keys, concurrent assignment/retirement, staff scope,
current-role removal and antiforgery.

## Verification

Before behaviour changes, all 114 existing Customer/geography/shared-reference
checks passed. Build succeeded with zero warnings/errors. All 34 existing
JavaScript checks passed. Complete bounded .NET verification passed all
**1,539 tests**: 143 API, 1,046 Identity admin and 350 website (including
31 new WI-017 scenarios), with zero failures/skips. No existing tests were
weakened, removed or skipped.
Local test evidence: `.artifacts/wi017-before`, `.artifacts/wi017-types`
and `.artifacts/wi017-full`; runtime artifacts are not committed.

Console validation confirms that only WI-017's status map entry changed; all
card/spec content and prior statuses are preserved. WI-017 is done. Main
integration requires a separate user request. WI-018 — Link contacts to locations
with one main contact each — is next, informational only.
