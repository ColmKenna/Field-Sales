# WI-016 — Customer and Location model checkpoint

The developer approved the implementation plan and Scenario Review matrix on
2026-10-02. They explicitly require a first Location when creating a Customer.
This document is the T-2.3.1 model review, before persistence and UI changes.

## Review question

Can each later area attach its data to a stable Location without replacing its
identity or changing the current ownership/geography relationships?

The proposed answer is yes: retain a required Customer ID and Town ID on each
Location, use the Location ID as every downstream reference, and add future
optional columns or related tables as those work items land. The future schema
will grow; existing core records and identifiers need no restructuring.

## Core records

| Record | Fields introduced by WI-016 | Invariant |
| --- | --- | --- |
| Customer | `Id`, `Name`, SQL `Version`; a Locations collection | Creation requires a valid first Location. Customer and first Location commit together. |
| Location | `Id`, `CustomerId`, `Name`, `NormalizedName`, `TownId`, optional `Eircode`, SQL `Version` | Exactly one Customer and one Town. Normal editing cannot change the Customer ID. |

IDs are GUIDs and do not change during edits. Names are required, trimmed,
at most 200 characters, and contain no control characters. Customer names
are not unique. Location normalized names use trimmed invariant uppercase;
their names are intentionally not constrained to be unique by SQL.
`(CustomerId, NormalizedName)` will be indexed for duplicate checks. The
warning applies to creation and renaming within the same Customer, excluding
the edited Location itself. The same name under another Customer needs no warning.

Eircode is optional bounded text, trimmed but otherwise preserved. The 20-character
limit is a storage/input bound, not a postal validity rule. Blank becomes null.
No lookup, coordinates or precision are invented in this slice.

The Customer factory creates its first Location through the same validation
path used for later additions. A failed first Location returns no aggregate.
The API will separately report an omitted `FirstLocation` object as a required
field error. The future store must save both records in one transaction; there
is no standalone customer-create path or Location deletion operation.

The records are defined in `FieldSales.Api/Directory/Customer.cs` and
`Location.cs`. API read/request contracts are in
`FieldSales.Directory.Contracts/CustomerContracts.cs`. They are not registered
in the DbContext, migrated, routed or shown in the website at this checkpoint.

## Planned persistence and command boundary

After review, add Customer and Location tables to existing DirectoryDb. Map
`Customer.Locations` through its backing field, with required Location-to-Customer
and Location-to-Town foreign keys using restrictive deletion. Index CustomerId,
TownId and the nonunique duplicate-check pair. Map both `Version` values to SQL
rowversions. No CountyId, RegionId or rep assignment is copied onto Location:
the current hierarchy is read through Town.

Create the Customer and first Location in one serializable transaction, after
all name/Eircode/Town validation. Add/edit a Location in the same transaction
as its Town availability and duplicate-name checks. An unconfirmed duplicate
returns exactly `This customer already has a location with that name` without
writing. The user may edit the name or explicitly confirm; recheck the current
data on confirmation. An edit submits its Location rowversion and checks it
even when the supplied values are unchanged.

Only Head Office role holders with the staff API scope may use these endpoints.
The BFF keeps bearer tokens on the server; forms have antiforgery and retain
inputs after validation/warnings. Customer record rows link to Location records
and show their current Town path. Type, master and closure columns remain empty.
No future-work-item names or disabled implementation controls appear in the UI.

Planned API operations:

- `GET /directory/customers` — Customer summaries and Location counts.
- `POST /directory/customers` — required Customer name and first Location.
- `GET /directory/customers/{id}` — Customer and its Location list.
- `POST /directory/customers/{id}/locations` — add a Location to that Customer.
- `GET /directory/locations/{id}` — Location, stored Customer ownership and Town path.
- `PUT /directory/locations/{id}` — edit name, Town and Eircode with a version;
  ownership is derived from the stored Location, never accepted in the request.

## Later additions and their attachment points

| Later area | Attachment | Preservation and future constraints |
| --- | --- | --- |
| Location Type (WI-017) | Optional `LocationTypeId` on Location | Classification can be added without replacing Location or making current records invalid. |
| Coordinates/precision/history (WI-019) | Position and history records keyed by `LocationId` | Defaults and captures preserve identity; retain old values for revert. Eircode and Town are already present as inputs. |
| Contacts/Main Contact (WI-018) | Location–Contact join records keyed by `LocationId` and `ContactId` | One person can serve many Locations; Main designation and replacement rules apply to links, not a single ContactId substituting for the collection. |
| Many Profiles/overrides (WI-102–103) | Location–Profile links and Location defaults/overrides keyed by `LocationId` | Many profiles remain possible; do not introduce a single ProfileId. Resolve each default separately with its source. |
| Master/Branch (WI-103) | Optional self-reference from Location to another Location | Same-Customer and cycle checks; no Customer-as-master entity. An additional composite key/FK can enforce matching Customer ownership. |
| Temporary closure (WI-139–140) | Closure-window records keyed by `LocationId` | A window keeps the Location active and can have an open-ended reopen date. Never overwrite it with a permanent state flag. |
| Permanent closure/reopen (WI-141) | Closure state/date/history on or related to Location | Keep records, contacts, orders and history. No Location deletion; reopening retains identity. |
| Coverage (WI-021 onward) | Reads Location ID, Customer ID and current Town hierarchy | Territory is derived; no stored County/Region/primary-rep shortcut in this slice. |
| Planning, calls, orders, performance | Foreign/domain references to stable `LocationId` | Location stays the measurement unit; the Customer remains the buying organisation. |
| Tiers (WI-063), customer range agreements | Customer-related records keyed by `CustomerId` | Attach to the organisation; Location master/range journeys remain separate. |
| Prospects/conversion (WI-173 onward) | Prospect-owned drafts in their own workflow, then Customer/Location creation | This slice owns Customer Locations. Conversion identity mapping and draft IDs remain a later design; no speculative nullable Customer ownership or polymorphic owner schema is introduced here. |

The attachment map covers the model checkpoint; none of the future fields,
tables or behaviours in this table are implemented by WI-016.

## Completing WI-015's real Location connection

Replace keyed `directory` `EmptyLocationUsageSource` with a real `locations`
provider that counts distinct Location records using the same DirectoryDbContext.
Count all stored Locations; closure later does not erase usage or history.
Require that source and preserve malformed/unavailable count rejection.

Call `GeographyStore.TownForAssignmentAsync` within the Location write's
serializable transaction. For edits, pass the existing Town ID from the saved
Location, never from a browser claim. An unchanged archived Town link survives;
a new link requires an active Town, County and Region. A missing/unknown Town
is reported as `Choose a town`. Existing records use the archived-labelled Town
projection. Restrictive Town foreign keys and shared transaction locks protect
against archive/delete races.

## Approved Scenario Review matrix

1. Create Hickey's Pharmacies with Hickey's Rathdrum, Rathdrum and A67 X123;
   reopen Customer and Location and verify listing/ownership.
2. Reject a missing first Location, saving no Customer or Location.
3. Reject a missing/unknown Town with `Choose a town`; keep input and save neither
   record when the first Location is invalid.
4. Warn on a same-Customer duplicate name with the exact sentence; save only
   after explicit confirmation or a changed name.
5. Apply that warning to renaming; exclude the edited row; allow the same name
   in another Customer. Case/outer-whitespace differences also warn.
6. Exclude archived Towns and ancestors from new selection; reject archive
   after form load. Empty geography offers a path to maintaining geography.
7. Preserve an existing archived Town during unrelated edits and display
   `(archived)` on the relevant part of its path.
8. Preserve required Customer ownership; reject invalid names, missing records
   and stale edits. Cancel a duplicate warning without adding/changing records.
9. Use real Location counts: Laragh with four stored Locations offers Archive,
   not Delete. Verify archive/reference retention and concurrent creation/retirement.
10. Verify access, role removal and antiforgery; migration preservation and
    persistence across restart. Eircode remains optional, and no coordinates
    are produced until WI-019.

Existing geography and shared reference scenarios run before changing their
behaviour. Implement these scenarios after the model review, then run the
complete .NET and JavaScript suites. Do not weaken or skip existing checks.

## Checkpoint state

The model, contracts and attachment map are ready for review. The existing
geography/shared-reference characterization passed all 75 tests, with no
failures/skips; TRX evidence is in `.artifacts/wi016-before`. The complete
solution build passed with zero warnings/errors. Plan-data validation confirms
only the WI-016 status entry changed, to active.

No Customer/Location database or UI behaviour has been added. This is a model
checkpoint, not completion of the five acceptance criteria. Their integration
tests and complete suite run follow persistence/UI implementation. Resume on
`Continue T-2.3.1`.
