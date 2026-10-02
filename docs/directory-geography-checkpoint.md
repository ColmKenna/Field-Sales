# WI-014 geography design

Task: T-2.1.1, DIR-US-007 scenarios 1–2. Plan and scenario matrix approved
on 2 October 2026. This document records the design checkpoint; it does not
claim that the implementation or acceptance tests are complete.

## Confirmed input routes

MI-16 is resolved by the owner's instruction: the initial geography will
either arrive as CSV or head office will enter it manually. Both routes
remain available. There is no mandatory seed file, automatic production
seed, external dataset dependency, or requirement to import before use.

## Model and persistence

Use a Customer Directory context in the existing staff API, independently
of the Catalogue context. `DirectoryDb` is an Aspire SQL Server database;
`DirectoryDbContext` owns its migrations. Development applies migrations;
production follows the existing pending-migrations check and deployment
process. No catalogue or identity tables move.

| Entity | Fields | Required relationship |
| --- | --- | --- |
| Region | Id, Name, NormalizedName, Version | None |
| County | Id, RegionId, Name, NormalizedName, Version | Exactly one Region |
| Town | Id, CountyId, Name, NormalizedName, Version | Exactly one County |

IDs are application-generated GUIDs with EF `ValueGeneratedNever`.
`Version` is a SQL rowversion for stale rename detection. Required foreign
keys use restrictive deletion. Unique indexes cover Region.NormalizedName,
(County.RegionId, County.NormalizedName), and
(Town.CountyId, Town.NormalizedName).

Names are trimmed, required, at most 200 characters, and compared without
case for duplicates within the same parent. Preserve display spelling and
diacritics. Duplicate Town names in different Counties are valid. API and
database uniqueness agree; racing submissions cannot create duplicates.

Renaming changes only a name, never IDs or parent links. There are no move
or retirement operations in WI-014. Stale saves return a conflict rather
than overwriting another user's rename.

### Later work fits

- WI-016 Locations store TownId in this directory context. Town choices
  return stable TownId, Town name, County and Region IDs/names, with a
  County-labelled display value; include Region when County names also
  need disambiguation.
- WI-021 derives coverage from Town → County → Region. Territory is not
  stored on the Town or a future Location as a duplicate derived value.
- WI-138 can move a Town by changing CountyId after its future impact and
  handover workflow. TownId and Location references remain stable. A County
  move similarly changes RegionId. Neither operation is exposed now.
- WI-015 can add archival state and connect directory usage to the shared
  reference-usage contract. Current IDs and parent relationships remain.
- WI-019 adds Town coordinates and Location coordinate precision/history.
  Geography names alone do not supply map coordinates. Coordinate provider
  and data decisions remain with that work item; WI-014 makes no claim to
  implement coordinate defaulting.

## CSV contract

UTF-8, optionally with a BOM, comma-delimited, with this exact header:

```csv
Region,County,Town
Leinster,Wicklow,Rathdrum
Leinster,Wicklow,Arklow
Leinster,Wicklow,Laragh
```

One record names a complete path. Names use the same validation as manual
entry. Quoted fields, escaped quotes, Unicode and CRLF/LF are supported.
Ignore empty records; reject malformed quoting, missing/extra columns,
missing headers, empty data files and invalid names with a row error.
Limit uploads to 1 MiB and 10,000 data records; report an exceeded limit
without saving anything. Region-only and County-only entries can be entered
manually; the initial Town import requires all three columns per record.

Validate the entire file before writes. In a single transaction, create
missing Regions, Counties and Towns and reuse existing matching paths.
Repeating an import preserves existing IDs and creates no duplicates.
Repeated paths within a file are harmless. Existing display names are not
renamed, existing records are not moved, and absent file entries are not
deleted. Report the counts added at each level and Town paths already
present. Roll back the whole import on a write error or concurrent conflict;
the user can safely retry the same file. No upload is required for manual use.

## Observable UI and API

- `/HeadOffice/Geography` lists Regions. Opening a Region lists its Counties;
  opening a County lists its Towns. The path links back to each ancestor.
- Each level provides creation and renaming with labelled fields, retained
  inputs on validation failures, clear empty states and accessible errors.
- A Town without a valid County is rejected with exactly `Choose a county`.
  A County without a valid Region is rejected with `Choose a region`.
- Head office can upload one CSV from the geography page and see its result
  or row errors. Preserve the current hierarchy after a failed upload.
- `/directory/geography` API operations serve the hierarchy, creation,
  renaming, Town choices and CSV import. The browser uses the Razor Pages
  BFF; bearer tokens stay server-side.
- Head Office User authorization and the staff API scope protect all these
  endpoints. Existing current-role lookup applies, including role removal.
- No fake Location counts, move buttons, archive actions or territory
  assignments are shown before their work items exist.

## Approved scenario matrix

| Scenario | Intent |
| --- | --- |
| Should_OfferTown_When_HierarchyIsCreated | Persist Leinster → Wicklow → Rathdrum and expose the County-labelled Town choice through API and head-office flow. |
| Should_RejectTown_When_CountyMissingOrUnknown | Exact Choose a county error, with no Town write. |
| Should_RejectCounty_When_RegionMissingOrUnknown | Enforce a real Region and no County write. |
| Should_DisambiguateTowns_When_NamesMatchAcrossCounties | Allow both Towns and distinguish their selection labels. |
| Should_PreserveIdentity_When_GeographyIsRenamed | Preserve IDs and parents; update paths/choice names; reject stale renames. |
| Should_RejectDuplicateName_When_SiblingAlreadyExists | Case-insensitive duplicate creation/rename protection, including concurrent creation and required/length validation. |
| Should_LoadHierarchy_When_FileIsValid | Exercise quoted/Unicode CSV, matching manual records and repeat import without duplicates. |
| Should_SaveNothing_When_ImportContainsInvalidRows | Reject malformed/invalid/oversized files and preserve all existing data. |
| Should_DenyChanges_When_HeadOfficeAccessIsMissing | Page and API boundaries, CSRF protection and revoked role; no unauthorized writes. |
| Should_PreserveHierarchy_When_ApplicationRestarts | Migrate real SQL and prove durable hierarchy and IDs after restart. |

WI-016 owns the actual Location creation screen. WI-014 acceptance proves
the Town-choice contract it will use, without claiming Location creation
already exists. Preserve existing catalogue behavior with characterization
checks before connecting the new host registrations and head-office link.
Run the full .NET suite using the repository's bounded SQL settings, all
existing JavaScript checks and a warnings-as-errors solution build.

## Planned files

- `FieldSales.Directory.Contracts/`: shared immutable geography requests,
  hierarchy items, choices and import results; project added to solution,
  API and website references.
- `FieldSales.Api/Directory/`: entities, context/factory/migrations,
  geography operations, CSV parsing and protected endpoints.
- `FieldSales.Api/Program.cs`, `FieldSales.AppHost/AppHost.cs`: context,
  database, service, authorization and endpoint registration.
- `FieldSales.Web/Directory/`: server-side staff API client.
- `FieldSales.Web/Pages/HeadOffice/Geography/`, head-office home and web
  registration: navigation, forms and upload.
- `FieldSales.Api.Tests/`, `FieldSales.Web.Tests/`: agreed scenarios and
  catalogue characterization; real SQL integration through the existing
  test-host conventions.
- `plan_docs/.agent-notes/WI-014.md` and this document: decisions and evidence.
- `plan_docs/field-sales-delivery/plan-data.js`: only WI-014 active/done map.

## Checkpoint state

Model and initial-load format are designed. No runtime feature is claimed
at this point. On 2 October 2026 the owner explicitly selected:
"Complete every step of WI-014 without another routine approval pause."
This overrides the original `Continue T-2.1.1` routine design pause for this
item. Continue the approved WI-014 plan, asking only about material unforeseen
business decisions. Other work items and main integration remain separate.

## Completion verification — 2 October 2026

The feature is implemented in API/model/import milestone `a40c798` and
website/scenario milestone `c9b96db`. The final bounded
solution test run exited 0: 143 API + 1,046 identity + 256 website tests,
**1,445 passed**, no failures or skips. All three TRX reports confirm each
test was executed and passed, under `.artifacts/wi014-full/`.

The warnings-as-errors solution build finished with **zero warnings and
errors**. All **34 JavaScript checks** passed, including 11 console status
checks. The 30 new geography cases cover the agreed scenario matrix;
existing tests were preserved. Two existing test file operations now spell
`System.IO.Directory` explicitly to avoid collision with the new Directory
contract namespace; their assertions and behavior are unchanged.

### Acceptance criteria, quoted

| Criterion | Result and evidence |
| --- | --- |
| 1. Creating Region "Leinster", County "Wicklow" under it and Town "Rathdrum" under Wicklow succeeds and Rathdrum is offered when creating a Location. | **Pass for the approved WI-014 slice.** `Should_OfferTown_When_HierarchyIsCreated` creates the hierarchy through the real head-office forms and verifies the persisted, County-labelled Town choice with stable IDs. The Location creation screen belongs to WI-016; this item supplies its shared contract and BFF method, as agreed in the approved plan. Location creation itself is not claimed as implemented here. |
| 2. Creating a Town with no County is rejected with "Choose a county". | **Pass.** `Should_RejectTown_When_CountyMissingOrUnknown` tests missing and unknown parent IDs against real SQL. Exact message checked, no data saved. |
| 3. A County belongs to exactly one Region; a Town to exactly one County. | **Pass.** Required SQL foreign keys and restrictive relationships in InitialDirectory; missing/unknown Region and County scenarios reject writes; hierarchy and choice tests check parent IDs. Rename tests preserve all relationships. |
| 4. Two Towns may share a name in different Counties and are shown with their County to tell them apart. | **Pass.** `Should_DisambiguateTowns_When_NamesMatchAcrossCounties` creates Newtown in Wicklow and Wexford and verifies distinct IDs and County/Region-labelled choices. Same-parent duplicate creation, rename and concurrent creation are rejected. |
| 5. An initial geography can be loaded from a file in one step (source to be confirmed, MI-16). | **Pass.** MI-16 is resolved: CSV or manual entry. `Should_LoadHierarchy_When_FileIsValid` submits a CSV through the real browser/BFF/API flow, adds missing places and reuses manual records. Repeating it preserves IDs and creates nothing. Invalid rows, malformed quoting, excess size/count and non-UTF-8 inputs are rejected; existing geography remains intact. |

Rename, stale-save retained-input, persistence after restart, Head Office
permissions, SysAdmin-only denial, revoked-role and CSRF scenarios also pass.

### Layout evidence and limits

Generated real Razor HTML previews are under `.artifacts/wi014-preview/`.
The desktop screenshot at 1440 × 1100 was inspected: path, Town/County
label, rename control, creation form and import entry are visible and styled.
The standalone Edge narrow screenshot was cropped because its viewport did
not reliably match the requested width; it is not evidence of a verified
mobile layout. The in-app browser then refused `file:` preview navigation
because only HTTP/HTTPS protocols are permitted. No alternate route was used
to circumvent that restriction. An exact-width mobile visual inspection
therefore remains unverified; functional HTML/form tests and the complete
regression suite pass. No browser viewport override remains applied.

### Final audit

- Strict Region → County → Town and both user-confirmed input routes are
  implemented. `docs/directory-geography.md` explains use and deployment.
- No outstanding business decision for this slice. Stack/code placeholders
  are resolved; MI-16 confirmation and the checkpoint override are recorded.
- WI-015, WI-016, WI-019, WI-021 and WI-138 behavior remains with those tasks.
- Provisional commit intent reconciles with the delivered model, one-step
  load, protected API and observable head-office forms. Actual commits use
  the card's WI-014 format and exact reference trailer.
- Only WI-014's active/done map entry changes in `plan-data.js`; console
  card, story, wave and decision data is unchanged. No other work item starts.
- Integration into main is separate. Next work item: **WI-015 — Archive a
  Town that is in use**, informational only.
