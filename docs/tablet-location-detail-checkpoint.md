# WI-035 — Location screen checkpoint

Prepared 29 October 2026 for T-4.2.2 / REP-US-004. The developer approved the plan,
scenario list and Apply. **Checkpoint review is pending.** Resume only with
`Continue T-4.2.2`. WI-035 remains `active`.

## Approved scope decisions

- Location-related Unsent work uses `locationId`, a GUID string in its JSON body.
  Existing bodies without it remain intact; names are never guessed into IDs.
- Keep the latest downloaded shop identity locally for the capturing rep while
  their Unsent work references it. The server cannot see the tablet's queue.
- No Call/Order history source exists yet. Show explicit history-unavailable
  states; populated history is deferred until its source exists.
- Record Call and New Order are equal primary buttons. Each explains its temporary
  unavailability when tapped, pending the later capture and order-entry screens.

These decisions were explicitly selected by the developer before plan approval.
They amend the populated-history and working-action expectations for this slice.
MI-62 (fully disabled account) remains outside this item.

## Implementation before this stop

`SnapshotStore.Locations.cs` reads the complete identity (Town, County, Eircode,
active Main Contact and contact details) from one active-generation query. This
uses the existing address contract: no street address is invented.

`LocationRetention` keeps only referenced identities in a rep-bound JSON file next
to the existing databases. It uses source-generated serialization for mobile
Release trimming and flushes a temporary file before replacing the cache. Capture
and activation share a workspace gate. Identity is saved before capture commits
and before an old active generation can be retired. A failed import cannot expose
staged identity. Normal cleanup can still retire whole snapshot generations.

An active assignment always wins over retained context. Otherwise, the Location
route returns retained details only while that rep's Unsent work references it.
Retained shops are absent from Home's assigned list and assigned-only search.
An Unsent Order produces exactly:

> No longer assigned to you — your existing order will still be sent

Both new-work buttons are absent in that state. Existing Unsent payloads are not
rewritten. The server snapshot contract/version and both SQLite schemas are
unchanged. The cache is within Android's existing backup-excluded app storage.

`LocationDetail.razor` retains the shared current-rep/24-hour guard. It clears old
details and temporary action messages before each access check and ignores old
worker reads after navigation. Main Contact stays in the identity block; inactive
contact status wording remains WI-036.

## Rendered checkpoint

The production Razor component and guard were compiled in the desktop component
project. An ignored preview program used real SQLite imports: assigned Doyle's
Shop, save an Unsent Order with its GUID, then import a snapshot without it.
`HtmlRenderer` produced both states; the production stylesheet was copied without
changes. These are actual-source static renders with synthetic data, not mockups.
Button interaction is verified separately by the component tests.

At **800 × 1100 CSS pixels**, browser geometry confirmed both assigned buttons at
**368 × 64**, on the same row with the same primary colour. The reassigned render
contains the exact notice and **zero buttons**. Neither state has horizontal
overflow. Both screenshots were visually inspected.

- Assigned: `.artifacts/wi035-preview/output/assigned.png`
- Reassigned: `.artifacts/wi035-preview/output/reassigned.png`
- Rendered HTML and stylesheet: `.artifacts/wi035-preview/output/`
- Reproducible fixture/render program: `.artifacts/wi035-preview/Program.cs`

No physical tablet was attached. No device installation, device data changes or
on-device execution is claimed.

## Verification so far

- **2 characterization tests passed before production edits.** They pin active
  rep-only reads, replacement, assigned-only search and unchanged Unsent data.
- **Tablet.Core: 91 passed, 0 failed, 0 skipped.** Includes retained identity after
  four further Syncs, cleanup and reopen; the latest downloaded contact; reassigning
  the shop back; cross-rep isolation; no-contact data; and legacy payload retention.
  The existing recovery and Unsent-file fingerprint tests also pass.
- **Actual-source component tests: 7 passed, 0 failed, 0 skipped.** Cover identity,
  equal actions, missing contact, history-unavailable states, both button events,
  exact reassignment notice/no buttons, expired access, rep change and overlapping
  route reads. The desktop adapter replaces only the MAUI entry/read boundary.
  The new project is included in the solution and supporting CI lane.
- **Android Release publish passed**, with no compiler warnings or errors in the
  successful log. The first publish's linker task-host process exited prematurely;
  a retry completed with the same source and build settings. Logs:
  `.artifacts/wi035-android-build.log`, `.artifacts/wi035-android-retry.log`.
- APK: `.artifacts/wi032/tablet-app/apk/com.fieldsales.tablet-Signed.apk`;
  SHA-256 `4A1D0424F40030CF6FEC6FEEF3E4D6E3B79F1185B2CD5E715379352324090EE3`.
- `git diff --check` passed.

## After checkpoint approval

Run the complete existing application suite and agreed checks, audit criteria 1–4
against the approved scope decisions, reconcile the final diff/commits, then set
WI-035 to `done` and push the completed feature branch. Do not integrate it.
WI-036 is next by console order, informational only.

## Missing information

None required for this approved slice. Repository resolves to
`D:/repos/Field-Sales` (`ColmKenna/Field-Sales`), integration branch to `main`,
source entry to `src/FieldSales.Tablet` and `src/FieldSales.Tablet.Core`, stack to
the approved .NET 10 MAUI Blazor Hybrid app. The console uses `workItemStatus`
instead of an inline `status-json`; only WI-035's entry changed.
