# WI-016 — Create a customer with its locations

Source T-2.3.1 / DIR-US-001. The user approved the Scenario Review plan and
required a first Location when creating a Customer. They accepted the model
checkpoint with `Continue T-2.3.1` on 2026-10-02. The original model and future
attachment map are preserved in `customer-location-checkpoint.md`.

## Delivered behaviour

Head Office → Manage customers and locations opens the Customer list. Create
customer requires the Customer name and first Location name/Town; Eircode is
optional. Both records save together. The Customer record lists its Locations,
links to their records and offers Add location. A Location record shows its
Customer, Town path and Eircode, and edits its name/Town/Eircode. Customer
ownership cannot be edited. Type, master and closure columns remain empty.

Required names are trimmed, bounded at 200 characters and reject control
characters. Customer names are nonunique. Location duplicates are checked
without case/outer-whitespace differences inside the same Customer, excluding
the edited record. A warning makes no changes; Save anyway explicitly confirms
it, while changing the name or cancelling remains possible. Input survives
validation and duplicate warnings. Eircode is trimmed bounded text, with no
postal validation or coordinate lookup. Blank Eircode is stored as null.

## Persistence, authorization and conflicts

- `CustomerStore` performs writes in the existing serializable DirectoryDb
  transaction helper. Creating a Customer without a valid first Location is
  rejected before persistence; there is no Customer-only create route.
- `20261002113453_AddCustomersAndLocations` adds two tables, restrictive required
  Location-to-Customer and Location-to-Town foreign keys, a nonunique
  `(CustomerId, NormalizedName)` duplicate-check index and a Town index. The
  composite index also supports Customer Location lists. Both records have SQL
  rowversions; an unchanged edit still checks and advances the Location version.
- Missing Towns return `Choose a town`. Newly assigned Towns require the complete
  hierarchy to be active. `TownForAssignmentAsync` runs in the same save
  transaction and derives the previous Town ID from the stored Location.
  An unchanged archived reference survives other edits and displays the archived
  component of its path. A claimed previous Town in a request cannot bypass this.
- `LocationTownUsageSource` replaces the empty `locations` provider under keyed
  `directory` registration, using the same scoped context as geography retirement.
  It counts all actual Location rows, including references to archived Towns.
  Required/malformed/unavailable source checks remain intact.
- The API group requires current Head Office access and staff API scope. The
  existing Razor Pages folder policy, current-role checks, server-held bearer
  tokens and antiforgery protect the BFF forms.
- Invalid fields return 400, missing records 404, stale versions 409 and duplicate
  warnings 409 with `RequiresDuplicateConfirmation`. SQL foreign-key/deadlock
  conflicts return a safe reload message. No rejected/cancelled form writes data.

API operations: GET/POST `/directory/customers`, GET
`/directory/customers/{id}`, POST `/directory/customers/{id}/locations`, and
GET/PUT `/directory/locations/{id}`. An edit request contains no Customer ID.

## Acceptance criteria and evidence

All five source acceptance criteria are checked through real website/BFF/API/SQL
scenarios in `FieldSales.Web.Tests/CustomerEndToEndTests.cs`.

| Source criterion, quoted | Verification |
| --- | --- |
| “Creating Customer \"Hickey's Pharmacies\" and adding Location \"Hickey's Rathdrum\", Town Rathdrum, Eircode A67 X123 saves both.” | `Should_SaveCustomerAndFirstLocationAndListThem_When_MinimumRecordIsEntered` submits the actual Customer form, follows its redirect, reads both records and verifies name/Town/Eircode/ownership. Invalid first-Location scenarios verify that neither record is saved. |
| “A Location without a Town is rejected with \"Choose a town\".” | `Should_SaveNeitherRecord_When_FirstLocationOrCustomerIsInvalid` checks missing, empty and unknown Town IDs with the exact message and no saved rows. `Should_RetainInputAndOfferGeography_When_CustomerFormCannotBeSaved` verifies the same feedback and retained form inputs through the BFF. Additional-Location and edit scenarios also reject missing Towns. |
| “A second Location named \"Hickey's Rathdrum\" in the same Customer shows \"This customer already has a location with that name\" and can be saved or renamed.” | `Should_WarnThenAllowCancelRenameOrSaveAnyway_When_LocationNameMatchesWithinCustomer` checks the exact warning, retained values, no write before confirmation, Cancel, Save anyway and a changed name. Rename/self-exclusion and concurrent-add cases check case-insensitive same-Customer warnings and permitted names under another Customer. |
| “Each Location belongs to exactly one Customer.” | Required SQL ownership FK, no editable ownership field, and the rename/forged-Customer-ID scenario prove retained ownership. `Should_RejectMissingOwnershipAndDeletionOfReferencedRows_When_DatabaseConstraintsAreUsed` proves rejection of null/missing owners and deletion of referenced Customer/Town rows. Missing routes and racing writes preserve integrity. |
| “The Customer record lists its Locations with Town (type, master and closure columns stay empty until their tasks land).” | The minimum-record scenario verifies Location links, Town path and exact empty Type/Master/Closure cells. The archive scenarios verify current archived Town/ancestor labels on Customer and Location pages, while active Town changes update the list and real usage counts. |

The additional approved scenarios cover optional Eircode, validation bounds,
duplicate confirmation/cancellation on rename, invalid/stale/no-op versions,
archival after form load, unchanged archived links, changed archived assignments,
real four-Location retirement counts, concurrent retirement and duplicate adds,
authorization/role removal/antiforgery, missing records, predecessor migration
preservation and persistence across API restart. The Customer fixture keeps the
production Location usage provider; older geography characterization fixtures
retain their explicit test provider. No existing assertions are weakened.

## Verification state

Before behavior changes, all 75 existing geography/shared-reference checks passed.
The focused integration run passed all 81 cases: 39 Customer and 42 geography,
with zero failures/skips. The complete solution build passed with zero warnings
or errors; all 34 JavaScript checks passed. Complete bounded .NET verification
passed all **1,508 tests**: 143 API, 1,046 Identity admin and 319 website,
with zero failures/skips. Local TRX evidence is in `.artifacts/wi016-before`,
`.artifacts/wi016-focused` and `.artifacts/wi016-full`.

WI-016 is done. Only WI-016's entry in the authorized `workItemStatus` map is
changed; no card/spec content is edited. Branch:
`feature/wi-016-create-customer-locations`. Scoped commits preserve the reviewed
model, persistence/API, screens, then tests/verification/handover.

Missing information: none. Types (WI-017), contacts (WI-018), coordinates
(WI-019), coverage (WI-021), tiers (WI-063), profiles/master (WI-102–103), closures
(WI-139–141), prospects (WI-173 onward) and bulk import remain outside this slice.
The next item is WI-017 — Maintain Location Type and Contact Type lists;
it has not been started. Main integration remains a separate user request.
