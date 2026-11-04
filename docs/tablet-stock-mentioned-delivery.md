# WI-050 — Record "Stock mentioned" on a phone call

T-5.3.2 / REP-US-006 S2 (amended), S8–S12; UX T6.5. Feature branch: `feature/wi-050-record-stock-mentioned-phone-call`.

On a phone Call the rep can't count the shelf, so the stock section becomes **Stock mentioned**: the same Suggested List rows
with Low, Out, Add to order and Add count. Only the products that were mentioned are saved.

## Scope and approved decisions

The developer replied **Approved** to the plan, decisions D1–D9 and the 12-scenario list, then **Apply**. At the checkpoint
(Phone rows and the save rule passing S11) the developer replied `Continue T-5.3.2`.

| # | Decision |
|---|---|
| D1 | `CallStockEntry` gains optional `low` (written only when true). Out is Low with a count of 0, no field of its own; "(k out)" counts Low entries with count 0. The Call stays version 1. |
| D2 | A phone Call saves only touched rows (Low, Out or a count). Untouched rows are not saved, never as Not checked. |
| D3 | A Low mark counts as recording something on either channel. |
| D4 | Review "Phone · 2 marked Low (1 out)", "(… out)" left out at 0; counted rows without Low add "· 1 counted". The section header shows the same, blank when nothing is mentioned. |
| D5 | Low marks are kept and saved on an In-person Call too, shown as a read-only "Low" / "Out" note until WI-051's tick; the In-person review adds "· 2 Low". Counts carry across both ways (Out shows 0 In person). |
| D6 | Out sets Low and a count of 0, replacing a count; Out off removes the 0 and keeps Low; Low off also clears Out. Add count opens a box with no stepper and the shared count checks; clearing it leaves the row untouched unless Low. |
| D7 | One "Filter or search" box narrows the rows by name or code, matching as the order search does. With no match it offers the downloaded catalogue with Add; the product becomes a row. No Add product button on Phone. |
| D8 | The server accepts `low` and refuses a phone entry with neither a count nor Low as `incomplete`. Phone is never the last stock check (WI-049 D4). |
| D9 | Finish: push, PR, merge after CI (merge commit, branch kept), update local main. |

## The Call's stock entries

```
"stockEntries": [ { "productId", "productCode", "productName", "unit", "count", "low" } ]   // count absent = Not checked; low only when true
```

- `CallContent` gains `Low` and `Out` counts, `RecordsSomething` includes Low, and `Problem` refuses a phone entry with neither
  count nor Low: "A product on a phone call needs Low, Out or a count". The tablet never sends one; the server applies the same
  check and refuses the Call as `incomplete`.
- An entry without Low serialises exactly as in WI-049.
- **Server store:** `ReceivedCallStockEntries` gains `Low` (bit, default 0), migration `AddLowToReceivedCallStockEntries`, so a
  Low-only phone entry isn't stored as Not checked. This column was added at the checkpoint, beyond the plan text, and shown to
  the developer there.

## Screen (T-06)

`RecordCall.razor` shows the stock section on both channels:
- **In person**: the WI-049 stock check, plus a read-only "Low" or "Out" note on marked rows; the header adds "· 2 Low".
- **Phone**: the heading reads **Stock mentioned** with "2 marked Low (1 out)". Each row has **Low** and **Out** toggles
  (`aria-pressed`), **Add to order** shown disabled, and **Add count**, which opens a count box with no stepper. There is no
  Remove and no Add product.
- **Filter or search** narrows the rows as typed. When nothing on the list matches, the catalogue is searched off the UI thread:
  "Nothing on the list matches 'vitamin'. From the catalogue:" with Add, or "No products match 'zzz'". An added row joins in name
  order and stays visible under the filter, ready to mark.
- Switching channel keeps every row, count and mark. A Low mark makes **Back to shop** ask first.

## Acceptance criteria and agreed scenarios

| # | Scenario | Evidence |
|---|---|---|
| 1 | S8: Phone at Doyle's shows Stock mentioned, the same 16 products and a filter box; each row Low, Out, Add to order, Add count, no stepper | `Should_ShowStockMentionedWithTheSame16ProductsAndAFilterBox_When_PhoneIsChosenAtDoyles` |
| 2 | S9: the filter narrows by name or code | `Should_NarrowTheList_When_ITypeInTheFilterBox` |
| 3 | S9: no match offers the catalogue; Add makes a row | `Should_OfferTheCatalogueAndAddTheProductAsARow_When_NothingOnTheListMatches` |
| 4 | S10: Out also marks Low with a count of 0 | `Should_AlsoMarkLowWithACountOf0_When_SudocremIsMarkedOut` |
| 5 | S11: review "Phone · 2 marked Low (1 out)"; only the 2 saved | `Should_ReviewPhone2MarkedLow1OutAndRecordOnlyThe2_When_SPF30IsLowSudocremOutAnd14Untouched` |
| 6 | S2 amended: Byrne's, 4 products mentioned, saved with Channel Phone and 4 entries | `SuggestedListTests.Should_SaveWithChannelPhoneAnd4Entries_When_FourProductsAreMentionedAtByrnes` (tablet save, upload, SQL Server, next snapshot) |
| 7 | S12: switching channel loses nothing | `Should_KeepEverythingEntered_When_SwitchingPhoneToInPersonAndBack` |
| 8 | Add count records only the count; "-2" → "Enter 0 or more" | `Should_RecordOnlyTheCount_When_AddCountIsUsedOnAPhoneRow` |
| 9 | Add count then Out sets the count to 0 | `Should_SetTheCountTo0_When_AddCountThenOut` |
| 10 | Phone with nothing marked: "Add pitch notes, a stock check or a competitor note" | `Should_ShowAddPitchNotesAStockCheckOrACompetitorNote_When_PhoneHasNothingMarked` |
| 11 | Low marks saved and shown In person after Phone | `Should_SaveLowMarksAndShowThem_When_TheCallIsSavedInPersonAfterPhone` |
| 12 | Server accepts Low marks and refuses an untouched phone entry | `TabletWorkPayloadTests.Should_AcceptLowMarksAndRefuseAnUntouchedPhoneEntry_When_ACallIsReceived` |

Mutation checks: Phone saving every row fails scenarios 5 and the changed WI-049 test; Out not marking Low fails scenarios 4, 5,
7, 9 and 11; removing the server rule fails scenario 12.

## Existing tests changed on purpose

- `Should_HideTheStockCheckAndKeepItsCounts_When_PhoneIsChosenThenInPerson` (WI-049 D5) became
  `Should_ShowStockMentionedAndKeepItsCounts_When_PhoneIsChosenThenInPerson`: Phone now shows the rows and saves the counted one.
- `SuggestedListTests` is now `partial` so scenario 6 shares its sync helpers.

## Not done here

Add to order from a call (shown disabled), the In-person Low tick and Low tab (T-5.5.1 / WI-051), competitor notes (T-5.8.1),
corrections (WI-055). Not checked on a device.
