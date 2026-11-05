# WI-054 — Mark unusually high quantities on Review

T-5.1.6 / REP-US-012 S14–S18; UX T7.11; US-NEW-004. Feature branch: `feature/wi-054-mark-unusually-high-quantities-review`.

On the order's lines, which is where the rep reviews an order and marks it ready, a line far above what the shop usually orders
of that product reads **"Usually about 48 here"**. This catches slips such as 48 typed as 480. The marker is text on the line
only: it never blocks Mark Ready to Send and never asks to be acknowledged.

## Scope and approved decisions

- The developer set the rule (MI-49).
- The developer replied **Approved** to the plan, D1–D7 and the 19-scenario matrix, then **Apply**.
- At the checkpoint (the rule passing R1–R12), the developer replied `Continue T-5.1.6`.

| # | Decision |
|---|---|
| D1 | The rule (the developer's choice). The history is the product's quantity on whichever of the shop's last 3 accepted orders contained it. With fewer than **2** such orders there is no marker. Usual means the **median** of those quantities (with two orders, their average). A line is marked when it is **more than 3×** usual. There is no absolute floor, and only high quantities are marked. The rule is one pure function, `QuantityMarker.For(line, history, step)`. |
| D2 | The text reads "Usually about 48 here". For Each products N is rounded to a whole number; for measured products, to the product's step, with the unit ("Usually about 2.5 kg here"). Halves round up. The 3× test uses the exact median. |
| D3 | Past quantities in a unit other than the line's are left out and don't count towards the 2. |
| D4 | Snapshot **1.5** (minor): each `recentOrderLines` row gains optional `quantities: [{ quantity, unit }]`, newest first (summed if the product was on an order twice). The tablet stores them in local format 5 (`recent_order_quantities`). |
| D5 | The marker shows on order-entry lines while the order is In Progress or Ready to Send, and not on Sent, Sending or Needs Attention orders. |
| D6 | Out of scope: Sync's order review, the Large annotation at head office (E28), low-quantity markers, and order acceptance (WI-067). Until WI-067 sets orders to Accepted, real tablets show no markers, as with the Suggested List's "Ordered recently". |
| D7 | Finish: push, PR, merge after CI (merge commit, branch kept), update local main. |

## Checkpoint: does the threshold catch a 10× slip without marking ordinary seasonal peaks?

Against a usual quantity of 48 (history 48, 50, 44):

| Line | Marked? |
|---|---|
| 480 | yes |
| 145 | yes |
| 144 | no |
| 120 | no |
| 4 | no |

- Every slip of more than 3× is marked, including 10× and an extra zero. A slip under 3× is not (48 typed as 84 is 1.75×).
- Seasonal peaks up to 3× are not marked; a peak over 3× is. The marker is text only.
- With no floor, a shop that usually takes 1 is marked at 4.

## Snapshot 1.5

```json
"recentOrderLines": [ { "locationId", "productId", "quantities": [ { "quantity", "unit" } ] } ]
```

- **Versions:** a 1.4 app ignores `quantities`. A 1.5 app reads a 1.4 snapshot with no quantities, so it shows no markers.
- **Server:** `ReadRecentOrdersAsync` reads the same last-3-accepted orders as before. It now orders them newest first (by date taken, then received) and sums each product's lines per order and unit.
- **Tablet:** local format 5 adds `recent_order_quantities`. The upgrade chain 1 → … → 5 keeps every snapshot and all Unsent work. Import refuses:
  - quantities in a snapshot older than 1.5;
  - a 1.5 row without quantities;
  - an empty list;
  - a quantity of 0 or less;
  - an empty unit.
- **Size:** a synthetic measurement at the target scale (500 shops × 48 recent order products, 1–3 quantities each) adds 1.77 MB of uncompressed JSON. That is more than the 0.6–1.4 MB estimated in the plan. Compressed, it adds 0.08 MB to the download. Import adds roughly 48,000 small rows; this was not timed on a device.

## Agreed scenarios

| # | Scenario | Evidence |
|---|---|---|
| R1–R12 | The rule | `QuantityMarkerTests` (unit, 12 tests) |
| U1 | S14 on screen: "Usually about 24 here" on the 240 line only | `OrderQuantityMarkerTests.Should_ShowUsuallyAboutNHereOnTheLine_When_OrderEntryShowsAnInProgressOrderWithAnUnusuallyHighLine` |
| U2 | S17: one tap, no question; the marker stays on Ready to Send | `Should_MarkTheOrderReadyToSendWithoutAsking_When_ALineIsMarked` |
| U3 | D5: no marker on a Sent order | `Should_ShowNoMarker_When_TheOrderIsSent` |
| T1 | S18: offline, from the imported history | `SnapshotOrderQuantitiesTests.Should_CalculateTheMarkerFromTheSnapshot_When_TheTabletIsOffline` |
| T2 | 1.5 import; 1.4 still imports; refusals; upgrade to format 5 | `SnapshotOrderQuantitiesTests` (4 tests, 8 cases) |
| S1 | Server: quantities newest first, Pending orders left out, units kept, summed per order | `SuggestedListTests.Should_SendEachRecentAcceptedOrdersQuantity_When_TheSnapshotIsBuilt` |
| E1 | End to end: three orders synced and accepted, then 480 marked against 48 and marked ready | `SuggestedListTests.Should_MarkTheLine_When_TheShopsLast3AcceptedOrdersAreSyncedAndAnOrderOf10TimesIsReviewed` |

Mutation checks:

| Mutation | Tests that failed |
|---|---|
| `>=` for `>` | R5, R7, R8 |
| One order counted as a pattern | R3, R11 |
| Mean for median | R1, R5, R6 |
| Units not compared | R11 |
| Server: oldest order first | S1 |
| Marker on every standing | U3 |

## Verification

- `dotnet build FieldSales.slnx -c Release --no-incremental`: no warnings.
- Full suite (SQL Server): 3,241 passed, 0 failed, including Tablet.Core unit 39, Tablet.Core integration 326, Tablet.Components 189 and Api 202.
- `node --test plan_docs/field-sales-delivery/tests/status-store.test.js`: 11 passed.
- Android Release build (`scripts/tablet-app.ps1 -Action Build -Configuration Release`): succeeded.

## Existing tests changed

- Snapshot version pins 1.4 → 1.5: two API assertions and two Tablet.Core contract assertions.
- One server assertion now compares (shop, product) pairs, because a recent order row now carries its quantities.
- Local format pins 4 → 5: the attribute, stock history, last Call gaps and contact status upgrade tests.
- The "newer app" format test now uses 6.

## Not done here

Sync's order review, the Large annotation (E28) and order acceptance (WI-067). Not checked on a device.
