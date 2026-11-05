# WI-057 — Add competitor notes to a call

T-5.8.1 / REP-US-018 S1–S5 and edge cases; design decision "Competitor Notes: free text with one link type". Feature branch:
`feature/wi-057-add-competitor-notes-call`.

On a call, a rep writes free-text notes about competitor products seen at the shop. Each note can link to our products (this
call's counted products first), one category or one range, and the notes count in the save review. Head office receives each note
with its links.

## Scope and approved decisions

- The developer answered three questions and replied **Approved** to D1–D10 and the 15-scenario list, then **Apply**.
- There was no checkpoint (Agent-Autonomous).

| # | Decision |
|---|---|
| D1 | Note model: `TabletCall` gains optional `competitorNotes`, a section version 1 of the call contract already reserved. Each note has an id, its text and one link type: None, Products (one or more), Range (one) or Category (one). Links keep their name as captured, so an archived Range still reads on old notes. Calls without notes are written as before. |
| D2 | A note counts as something recorded, so a call with only a note can be saved. The save review ends "1 competitor note" or "3 competitor notes". A note needs text, up to 4,000 characters (inferred). With nothing picked for its type it reads "Tick at least one product, or link to None", "Choose a category, or link to None" or "Choose a range, or link to None". |
| D3 | Record call: a collapsed "Competitor notes (N)" section after Pitch notes. Each note has "What did you see?", the four Link to options, the picker for the chosen type, and Remove note. "Add competitor note" adds another. Notes are kept through a resume, and work the same on new calls and follow-ups. |
| D4 | Pickers. Products: this call's products with a count or a Low mark first, then a search of every downloaded product. Restricted products are never downloaded; unavailable and outside-range products can be linked. Category: a filter over the downloaded categories by breadcrumb. Range: "No ranges on this tablet yet" until E9 brings Ranges; archived Ranges are never offered. |
| D5 | Switching link type with picks asks "Switching will remove 2 linked products" (or "…the linked category", "…the linked range"), with Switch and Cancel. Cancel keeps the picks. With nothing picked it switches straight away. |
| D6 | Edit Call (developer's answer): a note's text can change and a note can be removed. Its links stay as saved and no note can be added. `CallStore.Correct` refuses a new note, changed links or blank text. A call recording only notes can still be corrected. This completes US-015 S2, which WI-055 left for when notes existed. |
| D7 | The call page lists each note with what it is linked to. |
| D8 | Server: tables `ReceivedCallCompetitorNotes` and `ReceivedCallCompetitorNoteProducts`, added by migration `AddCompetitorNotesToReceivedCalls` (new tables only). The structure check refuses blank or over-long text, an unknown link type, link fields that don't match the type, Products with none or repeats, a Range or Category without an id, and a repeated note id. A check constraint keeps one link type per row. Names stay in the received body, as stock entries' do. |
| D9 | Out of scope: Ranges themselves (E9), competitor reporting (not designed), prospects (WI-173). |
| D10 | Finish: push, PR, merge after CI (merge commit, branch kept), update local main. |

## Criteria

| # | Criterion | How it is met |
|---|---|---|
| 1 | "Competitor 250ml SPF30 on shelf", Link to Products, both SPF30 sizes ticked from this call's list: 2 product links | Scenario 1 |
| 2 | "Rival suncare stand by the till", Link to Category "Suncare": 1 category link, no products | Scenario 2 |
| 3 | Link to None: no links | Scenario 3 |
| 4 | With 2 products ticked, switching to Range asks "Switching will remove 2 linked products" and can be cancelled | Scenario 4 |
| 5 | 3 notes on one call save separately and read "3 competitor notes" in the save review | Scenario 5 |
| 6 | The picker lists this call's counted products first; unavailable and outside-range products are linkable; restricted products never appear | Scenario 6. Every downloaded product can be linked, and restricted products are never downloaded (existing snapshot tests). |
| 7 | Range offers active Ranges from the snapshot (none until E9); archived Ranges never offered for new notes | Scenario 7 |

## Agreed scenarios

| # | Scenario | Evidence |
|---|---|---|
| 1 | S1: 2 product links | `CompetitorNoteScreenTests.Should_SaveTheNoteWith2ProductLinks_When_CompetitorSpf30IsLinkedToBothCountedSpf30Sizes` |
| 2 | S2: 1 category link, no products | `Should_SaveTheNoteWith1CategoryLinkAndNoProducts_When_RivalSuncareStandIsLinkedToSuncare` |
| 3 | S3: no links | `Should_SaveWithNoLinks_When_LinkToIsLeftAtNone` |
| 4 | S4: asks, Cancel keeps both, Switch clears them | `Should_AskBeforeRemoving2LinkedProductsAndKeepThemOnCancel_When_LinkToIsSwitchedToRange` |
| 5 | S5: 3 notes saved separately, "3 competitor notes" | `Should_SaveThreeNotesSeparatelyAndRead3CompetitorNotes_When_ThreeNotesAreAddedToOneCall` |
| 6 | D4: counted and Low products first, Not checked rows not; search finds the rest | `Should_ListThisCallsCountedAndLowProductsFirst_When_TheProductPickerOpens` |
| 7 | Criterion 7: no ranges to pick; a Range note can't be saved | `Should_OfferNoRangesAndRefuseTheNote_When_TheSnapshotHasNoRanges` |
| 8 | D2: a call with only a note saves | `CompetitorNoteContentTests.Should_SaveACallWithOnlyACompetitorNote_When_NothingElseIsRecorded` |
| 9 | D2: blank, too long, nothing picked | `CompetitorNoteContentTests.Should_RefuseTheNote_When_ItHasNoTextIsTooLongOrItsLinkTypeHasNoPick` (6 cases) |
| 10 | D3: notes and links come back after a resume | `CompetitorNoteScreenTests.Should_KeepTheCompetitorNotes_When_RecordCallIsRebuilt` |
| 11 | D6: Edit Call changes text and removes, adds nothing | `CompetitorNoteScreenTests.Should_ChangeNoteTextAndRemoveNotesButAddNone_When_EditCallHasCompetitorNotes` |
| 12 | D6 in storage: a new note, changed links or blank text refused | `CompetitorNoteCorrectionTests.Should_RefuseTheCorrection_When_ItAddsANoteChangesItsLinksOrBlanksItsText` (5 cases, one accepted) |
| 13 | D7: the call page lists each note with its links | `CompetitorNoteScreenTests.Should_ShowEachCompetitorNoteWithItsLinks_When_ASavedCallIsOpened` |
| 14 | D8: malformed notes refused | `CompetitorNotePayloadTests.Should_RefuseTheCall_When_ACompetitorNoteIsMalformed` (10 cases) |
| 15 | End to end: saved, uploaded, stored with links | `SuggestedListTests.Should_UploadEachNoteWithItsLinks_When_ACallWithCompetitorNotesIsSynced` |

Mutation checks:

| Mutation | Tests that failed |
|---|---|
| No confirmation before removing linked products | 4 |
| Every row on the call listed first | 6 |
| A correction may change a note's links | 12 (2 cases) |
| The server skips the link checks | 14 (5 cases) |
| A note doesn't count as recorded | 8, 9 (all cases) |

An early run of the mutation checks used a test filter on "Competitor", which missed scenarios whose names lack that word. Those
two mutations were rerun against the whole screen suite and are caught, as the table shows.

## Verification

- `dotnet build FieldSales.slnx -c Release --no-incremental`: no warnings.
- Full suite (SQL Server): 3,313 passed, 0 failed, including Tablet.Components 216, Tablet.Core 346, Tablet.Core unit 49, Api
  207 and Api unit 271.
- `node --test plan_docs/field-sales-delivery/tests/status-store.test.js`: 11 passed.
- Android Release build (`scripts/tablet-app.ps1 -Action Build -Configuration Release`): succeeded.

## Not done here

Ranges themselves (E9), competitor reporting, prospects (WI-173). Not checked on a device.
