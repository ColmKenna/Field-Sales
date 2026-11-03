# WI-047 — Record a call with channel, pitch notes and a save review

T-5.3.1 / REP-US-006 S1, S3–S5. Feature branch: `feature/wi-047-record-call-channel-pitch-notes`.

A rep records a Call at a shop by choosing In person or Phone and adding pitch notes, confirms a one-line summary, and the
Call is saved with its date and rep. It stays Unsent until the next Sync sends it.

## Scope and approved decisions

The developer replied **Approved** to the plan, decisions D1–D10 and the 13-scenario list, then **Apply**, lifting the
remaining stops and answering D11: push, open a PR, merge once CI passes, then update local `main`.

| # | Decision |
|---|---|
| D1 | Review dialog: "Save this call?", the summary on one line joined with " · ", **Back** and **Save** (story wording over the T-06 wireframe's lines and "Save call"). |
| D2 | Calls stay in the server's `ReceivedWork` ledger, with typed structure checks; no new table or migration. |
| D3 | The contract lives in a new `FieldSales.Calls.Contracts` project, shared by tablet and API. |
| D4 | "My name" is `capturingRep`, the rep's sign-in subject; names resolve from identity, as for orders. |
| D5 | Extension points are reserved optional sections documented in the contract, not always-null fields. |
| D6 | A Call at a shop reassigned since the snapshot saves and is accepted; Record Call is offered while the shop is assigned. |
| D7 | The draft is kept in app memory per rep, session and shop, so a resume restores it; leaving with work asks first. |
| D8 | Phone with nothing recorded gets the same message as In person. |
| D9 | Pitch notes are trimmed; blank counts as none; at most 4,000 characters. |
| D10 | This slice shows "In person · Pitch notes" / "Phone · Pitch notes"; the count parts of S3 arrive with WI-049, WI-051 and WI-057. |

## The Call model

```
{ "schema": "field-sales-call", "version": 1, "id", "locationId", "capturingRep", "calledOn": "yyyy-MM-dd",
  "savedAt": "<local time with offset>", "channel": "InPerson" | "Phone",
  "snapshot": { "version", "builtUtc" }, "pitchNotes": "…" }        // pitchNotes absent when there is no Pitch
```

| Later item | Where it attaches |
|---|---|
| Stock check (T-5.4.1), Stock mentioned (T-5.3.2) | `stockEntries[]` |
| Low marks (T-5.5.1) | `low` on each stock entry |
| Competitor notes (T-5.8.1) | `competitorNotes[]` |
| Follow-up (T-5.7.1) | `followUpOf` |
| Campaign outcomes (E20) | `campaignOutcomes[]` |
| Range Review (E23) | `rangeReview{}`; Call Purposes follow from the sections present |
| Visit Due completion (E13) | no field: derived from Location, rep and `calledOn` |

Each section is optional within version 1 and absent means empty, so no later feature reshapes the Call. `CallContent` holds
what both sides check (`Problem`) and the review line (`Summary`); each later section adds its part there. A saved Call is
immutable: `CallStore` has Save only (one id per page; a repeat returns the Call already saved), and corrections (T-5.6.1)
will add their own operation behind the upload claim from WI-046.

Server checks (`TabletWorkReceiver.ReadCall`): unreadable or an unknown channel → `corrupt`; another schema or a newer
version → `unsupported`; id mismatch, no Location, rep, date, channel or snapshot, nothing recorded, or notes over the limit
→ `incomplete`; another rep → `not-yours`. The directory is never consulted.

## Screen (T-06)

`RecordCall.razor` at `/locations/{shop}/calls/new`, opened by **Record Call** on the Location screen. The page has:
- **How**: two large options, In person and Phone, neither selected. The chosen one is marked by more than colour.
- **Pitch notes**: a collapsed section with a text area.
- **Review and save**.

A missing channel or an empty Call is explained on the page, and the review doesn't open. The review dialog shows the summary
with **Back** and **Save**. After saving, the shop screen says "Call saved. It goes to head office at your next Sync."
**Back to shop** with work entered asks "Leave without saving this call?" (**Leave** / **Stay**). The Unsent review names the
channel as "In person" / "Phone".

## Acceptance criteria

| # | Criterion | Evidence |
|---|---|---|
| 1 | Record Call at Murphy's opens one scrolling page with In person and Phone, neither preselected | `Should_ShowChannelAsTwoUnselectedOptions_When_RecordCallOpensAtMurphysPharmacy`, `LocationDetailComponentTests.Should_OpenRecordCallForTheShop_When_RecordCallIsTapped` |
| 2 | In person + "Presented autumn cold & flu range", confirmed, saves with date, rep, channel and notes | `Should_SaveTheCallWithDateRepChannelAndNotes_When_AnInPersonPitchAtMurphysOn22September2026IsConfirmed` (store: 22 Sep 2026, `colm`, In person, notes, snapshot; screen: confirmed through the review) |
| 3 | Save opens a review summarising the call with Save and Back | `Should_SummariseInPersonPitchNotesWithSaveAndBack_When_ReviewAndSaveIsTapped` (screen and summary rule) |
| 4 | Notes but no channel: not saved, "Choose In person or Phone" | `Should_NotSaveAndShowChooseInPersonOrPhone_When_NotesButNoChannel` (store and screen) |
| 5 | A channel with nothing recorded: "Add pitch notes, a stock check or a competitor note" | `Should_ShowAddPitchNotesAStockCheckOrACompetitorNote_When_AChannelButNothingRecorded` (store and screen, both channels, blank notes) |
| 6 | A saved Call is Unsent until the next Sync | `Should_StayUnsentThenUploadAndMarkSent_When_TheRepSyncs`, `Should_StoreTheCallOnce_When_ARecordedCallIsUploadedTwice`, and the end-to-end upload test through the real API |

Edges: `Should_KeepWhatWasEnteredAndSaveNothing_When_BackIsTappedInTheReview`, `Should_SaveAndAcceptTheCall_When_TheShopWasReassignedAfterTheSnapshot`
(store and API), `Should_AskBeforeLeaving_When_TheRepLeavesWithUnsavedWork`, `Should_RestoreTheDraft_When_TheAppResumesMidCall`,
`Should_SaveOnce_When_SaveIsTappedTwice` (store and screen), `Should_RefuseTheCall_When_ItsStructureIsWrong` (13 cases, plus
not-yours and nothing-recorded through the API).

Mutation check: making "anything recorded" always true fails all three nothing-recorded store cases; restored.

## Existing tests changed

These pinned the server's lenient Call acceptance or the Record Call stub, and change on purpose:
- `TabletWorkPayloadTests`: `Should_AcceptTheCall_When_ItIsAnObjectNamingNoOtherId` and `..._RefuseTheCall_When_ItIsUnreadableOrNamesAnotherId`
  became `Should_AcceptTheCall_When_ItIsARecordedCall` and scenario 13.
- `TabletUploadApiTests`: the Call is a recorded Call (stored once, scenario 12); id mismatch uses a real Call; "another kind"
  sends a real Call and still gets 409.
- `TabletUploadEndToEndTests`: records its Call through `CallStore`.
- `LocationDetailComponentTests`: "Record Call isn't available" became the navigation test.
- The component screen harness gains typing, attribute reads, and rebuilds with the page's own parameters.

## Out of scope

Stock check (WI-049), Stock mentioned (WI-050), Low (WI-051), competitor notes (WI-057), corrections (WI-055), follow-ups
(WI-056), campaign outcomes (E20), Range Review (E23), Visit Due completion (E13), Home's record of the day (WI-048), Recent
calls on the Location screen.

## Verification

All on the feature branch before the PR, 3 November 2026:

- Release build of `FieldSales.slnx` (`--no-incremental -m:1`): 0 warnings, 0 errors.
- Complete suite (`dotnet test FieldSales.slnx -c Release --no-build --no-restore -m:1 --settings sqlserver.runsettings`, Docker
  running): **3,090 passed, 0 failed, 0 skipped** across 12 projects. Tablet Core integration 273 (was 263), Tablet Components
  115 → 125, API unit 247 → 252, API integration 186 → 189.
- Console status-store tests: 11 passed.
- Android Release build (`scripts/tablet-app.ps1 -Action Build -Configuration Release`): succeeded; the package now carries the
  Calls contract assembly.
- Not checked on a device: T-06 was not seen on the Samsung tablet. Worth a look at the two channel options, the collapsed
  Pitch notes section, the review dialog above the fixed footer, and returning from the dialler mid-call.
