# WI-049 — Run a stock check from the suggested list

T-5.4.1 / REP-US-007 S1, S3–S6 and edge cases. Feature branch: `feature/wi-049-run-stock-check-from-suggested`.

A Stock Check opens on the products counted last visit plus those on the shop's last three Accepted Orders, each labelled by
source. The rep adds or removes rows, and uncounted lines save as Not checked, so they are on the next visit's list.

## Scope and approved decisions

The developer replied **Approved** to the plan, decisions D1–D11 and the 19-scenario list, then **Apply**. At the checkpoint
(suggested-list function passing S1, snapshot 1.3 fields, size figures) the developer replied `Continue T-5.4.1`.

| # | Decision |
|---|---|
| D1 | `OrderLifecycleStatus.Accepted` exists with no transition until WI-067; "Ordered recently" reads Accepted orders only. Until WI-067 ships, real lists show only "Last visit". Tests mark orders Accepted directly. |
| D2 | MI-12 stopgap: `TabletSnapshot:SuggestedListOrderCount` (API configuration, default 3, validated 1–10 at start-up). No settings screen. |
| D3 | Snapshot 1.3 adds two optional (shop, product) families, filtered to the rep's shops and visible products. The tablet merges them in one function, `SuggestedList`. |
| D4 | Server tables `ReceivedCalls` and `ReceivedCallStockEntries`, written in the Call's receive transaction. Last stock check = the shop's newest In-person Call with stock entries, by saved time, whoever recorded it. No backfill. |
| D5 | The stock check is hidden on Phone. Its rows stay in the draft if the rep switches back; a Phone call saves no stock entries (T-5.3.2's "Stock mentioned" later). |
| D6 | A stock check where every line is Not checked doesn't count as recording something. |
| D7 | Count: typed amount with − and + (steps of 1, never below 0). Below zero: "Enter 0 or more". A fraction on a per-item product: "Enter a whole number". Measured products accept up to 6 decimals and show their unit. |
| D8 | Story wording: "Last visit", "Ordered recently", "Last visit · Ordered recently"; "10 counted, 3 not checked" ("13 counted" when all counted); "No suggested products yet — search or browse to add". Rows in name order. |
| D9 | Each stock entry keeps product id, code and name as captured, the unit, and the count (absent = Not checked). Removed rows aren't saved. |
| D10 | Size checked by a desktop measurement at target scale; not timed on a device. |
| D11 | Finish: push, PR, merge after CI (merge commit, branch kept), update local main. |

## Snapshot 1.3 (minor; nothing removed or changed)

```
"lastStockCheckLines": [ { "locationId", "productId" } ],   // the shop's last stock check, counted or Not checked
"recentOrderLines":    [ { "locationId", "productId" } ],   // products on its last N Accepted Orders, once per shop
"manifest.counts": { …, "lastStockCheckLines", "recentOrderLines" }
```

- The builder reads the history from the Ordering database after the directory and catalogue reads. It keeps only the rep's
  shops and products included in this snapshot, so a restricted product the rep may not see never appears.
- Recent orders are ranked per shop by date taken, then time received, then id. SQL Server answers this with a correlated
  count over the new `(LocationId, Status, TakenOn)` index. The order lines are read from the received bodies.
- A 1.2 app ignores the new families. A 1.3 app reads 1.2 snapshots with an empty history.
- The tablet stores the history in local format 3 (two tables; upgrade chain 1 → 2 → 3 keeps every generation and all Unsent
  work). Import refuses a 1.3 snapshot whose history row names a product or shop outside it, a repeated row, a missing
  family or wrong counts; the previous day stays active. Old generations' history is cleaned up with the rest.

### Size at target scale (desktop, Release, random ids)

500 shops, 25,000 products, 75,000 prices and 75,000 attributes, with and without 119,617 history rows (about 96,000
last-check rows and 24,000 order rows):

| | 1.2 (no history) | 1.3 (with history) |
|---|---|---|
| Download (gzip) | 2.91 MB | 5.66 MB |
| Plain JSON | 22.2 MB | 34.8 MB |
| Desktop import (warm) | 4.7–5.7 s | 6.6–6.8 s |
| Local snapshot database | 35.5 MB | 66.4 MB |

Not timed on the Samsung, where WI-033's full Sync took 50–58 s against the 90 s budget.

## The Call's stock entries

```
"stockEntries": [ { "productId", "productCode", "productName", "unit", "count" } ]   // count absent = Not checked
```

`CallContent` gains the counts: `Counted`, `NotChecked`, `HasStockCheck` (at least one counted), the shared count check and
the summary part. The server refuses a Call as `incomplete` when an entry has no product, a blank code or name, an unknown
unit or a repeated product, or when a count fails the shared check. The product is never checked against the catalogue.

## Screen (T-06)

`RecordCall.razor` adds a **Stock check** section between **How** and **Pitch notes**, shown unless Phone is chosen:
- The header shows "10 counted, 3 not checked" and **Add product**.
- Each row shows the product name, its source label, − / count / +, the unit for measured products, and **Remove**. A blank
  count reads "not checked". An invalid count shows its message under the row and stops the review.
- **Add product** opens a search of the downloaded catalogue, or category browsing when nothing is typed. A product already
  on the list reads "On the list". Added rows join in name order.
- A shop with no history shows "No suggested products yet — search or browse to add".
- The draft carries the rows, so an app resume restores them. Changed rows or any count make **Back to shop** ask first.

## Acceptance criteria

| # | Criterion | Evidence |
|---|---|---|
| 1 | Murphy's last check (10 counted, 2 Not checked) and last 3 Accepted Orders (5 products, 3 on the check) give 14 products, each once, labelled | `SuggestedListTests.Should_Show14ProductsEachOnceLabelled…` through the real API, SQL Server and tablet import |
| 2 | Removing 2 and adding Vitamin D by search or browsing leaves 13 | `Should_Have13Products_When_2AreRemovedAndVitaminDIsAddedBySearch`, `Should_AddTheProduct_When_ItIsChosenByBrowsingCategories` |
| 3 | 13 with 3 uncounted: review "10 counted, 3 not checked"; the 3 save as Not checked and are on the next list | `Should_ShowTenCountedThreeNotCheckedInTheReview…`, `Should_SaveThe3AsNotChecked_When_TheCallIsSaved`, `Should_PutTheNotCheckedProductsOnTheNextVisitsList…`, `Should_KeepTheProductOnTheList_When_ItIsNotCheckedAgain` |
| 4 | -2 rejected with "Enter 0 or more"; measured products accept decimals with their unit | `Should_RejectTheCountWithEnter0OrMore_When_MinusTwoIsEntered`, `Should_AcceptADecimalCountWithItsUnit_When_TheProductIsSoldByMeasure`, server `Should_RefuseTheCallAsIncomplete_When_AStockEntryIsMalformed` |
| 5 | Doyle's with no history: "No suggested products yet — search or browse to add" | `Should_ShowNoSuggestedProductsYetSearchOrBrowseToAdd…`; S1 test asserts Doyle's list is empty |
| 6 | A not-yet-Accepted order doesn't feed the list; restricted products never appear | `Should_LeaveTheOrderOut_When_TheMostRecentOrderIsNotYetAccepted`, `Should_NeverListTheProduct_When_ARestrictedProductIsOnAnOldOrderOrTheLastCheck` (permitted and not) |

## Existing tests changed on purpose

- Snapshot version pins 1.2 → 1.3 (two API assertions, two Tablet Core contract assertions); the API test reader counts
  the history families.
- Local format pins 2 → 3; the "newer app" refusal now writes format 4; the "unknown newer minor" fixture uses 1.4; the
  format-1 upgrade fixture also drops the two history tables; `Snapshots.Build` adds empty history for 1.3 and later.

## Not done here

S2's Unavailable / Outside your ranges labels (E8, E9), Low marks and Add to order (T-5.5.1), the low-stock hint (E12),
Stock mentioned (T-5.3.2), accepting orders (WI-067), a settings screen (MI-12), corrections (WI-055). Not checked on a
device.
