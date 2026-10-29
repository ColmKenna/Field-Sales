# WI-038 — Take a standalone order offline and mark it ready to send

T-5.1.1 / REP-US-012 S4, S5, S6, from `main` at `588e5b7` on
`feature/wi-038-take-standalone-order-offline-mark`.

## Approved scope and decisions

The developer approved the plan, the 25-scenario matrix and decisions D1 to D9 with
**Approved** on 29 October 2026. After increment 0 they instructed: "Continue until work
complete, if there are any questions ask now". That lifted the stop after each increment.
Work continued through increments 1 to 5 with a commit and a notes update at each.
No product values remain unresolved for this slice.

| # | Decision |
| --- | --- |
| D1 | The same product added twice is **one line**, quantities summed and revalidated. |
| D2 | A line captures the winning price, its source and the list of candidates. Only the base price is a candidate now, so E6 adds candidates without changing the stored shape. |
| D3 | A line stores the product code and name at capture. |
| D4 | The snapshot reference (contract version and `BuiltUtc`) is on the order and on each line. A line added after a later Sync follows the newer snapshot. |
| D5 | The server model is the same `FieldSales.Ordering.Contracts` records the tablet writes. There is no endpoint, storage or validation yet (WI-039). No snapshot version bump. |
| D6 | A product with no base price in effect cannot be added ("No price yet"). |
| D7 | "Today" is the tablet's local date. The price is the newest `EffectiveFrom` on or before it. |
| D8 | The Unsent count is a core `UnsentSummary`, shown as one line on the Sync screen. Home waits for WI-048 and the list for WI-042. |
| D9 | Nothing is stored until the first line is added. Each add saves at once as In Progress. New Order always starts a new order. A wrong line or a left-behind In Progress order cannot be removed or reopened until WI-046. |

Refinements made while implementing, all inside the decisions above:

- A merged line keeps the price, snapshot and unit it captured first. Only its quantity changes.
- A new line must meet the product's minimum and step. Adding to a line needs a positive whole number of steps,
  and the new **total** must meet the rules. (A test showed that checking the added amount against the minimum
  would have refused 0.5 kg on top of 1.5 kg of a 1.0 kg-minimum product.)
- Adding needs the shop to be assigned on the current snapshot, for new and existing orders alike. Marking an
  order ready needs no connection and no assignment, because an order is judged against the snapshot it was built from.
- The screen chooses the order id when it opens, so a repeated add cannot start a second order. A second tap
  while a save is running is ignored.
- Quantity text accepts digits and a decimal point only, so "1,5" is refused instead of being read as 15.

## The order record

The body stored for an Unsent Order, and uploaded unchanged by WI-039, is `TabletOrder` in
`src/FieldSales.Ordering.Contracts/OrderContract.cs`:

```
{ "schema": "field-sales-order", "version": 1, "id", "state": "InProgress" | "ReadyToSend",
  "locationId", "orderedById", "capturingRep", "takenOn": "yyyy-MM-dd", "callId": null,
  "snapshot": { "version", "builtUtc" },
  "lines": [ { "id", "productId", "productCode", "productName", "quantity", "unit",
               "price": { "amount", "kind", "label", "candidates": [ { "kind", "label", "amount", "isWinner" } ] },
               "snapshot": { "version", "builtUtc" } } ] }
```

`locationId` stays a top-level property because `LocationRetention` reads it from any Unsent item, which keeps a
reassigned shop's details while the order waits. Later items should add fields only as optional, additive
properties. A tablet-written body is locked three ways: property names in a Tablet Core test, exact round trip and
tolerance of an added optional field in API unit tests, and the contract's JSON source generation for trimmed
Android builds.

## What was built

| Inc | Commit | Work |
| --- | --- | --- |
| 0 | `e652ee2` | Branch from `origin/main`, `WI-038` set to `active`, handover notes, green baseline. No new characterisation test was needed: existing tests pin retention, legacy payloads, sign-out counts and record equality. |
| 1 | `016ab3e` | `FieldSales.Ordering.Contracts`; `OrderStore.AddLine` and `Find`; `SnapshotStore.ReadOrderableProduct`; `UnsentStore.Find`; a shared breadcrumb helper. |
| 2 | `341f068` | `FieldSales.Api` references the contract; three unit tests read a tablet body into the server model. |
| 3 | `2fe0359` | `ProductSearchIndex`, facade methods, the `NewOrder` page, and the Location screen's New Order button. |
| 4 | `26a7de8` | `OrderStore.MarkReadyToSend`, `UnsentStore.Summarise`, the Mark Ready to Send control, and the Unsent line on Sync. |
| 5 | this commit | Verification, this record, and the `done` status. |

## Acceptance criteria

> Opening Byrne's Chemist, choosing New Order, adding "Vitamin D 1000IU 90s" quantity 12 and saving creates an In Progress Order dated today with no Call linked (S4)

The Location screen's **New Order** opens `/locations/{id}/orders/new`
(`Should_OpenTheNewOrderScreenForTheShop_When_NewOrderIsTapped`). Searching finds the product offline
(`Should_FindProductsByNameOrCodeIgnoringCaseAndAccents_When_TheRepSearchesOffline`), the rep enters a quantity and
adds it, and the order is saved at once. `Should_SaveAnInProgressOrderDatedTodayWithNoCallLinked_When_IOpenByrnesChemistChooseNewOrderAddVitaminD1000IU90s`
checks state, date, no Call, the line and the Unsent record, and `Should_SaveTheLinesOfOneOrderAndShowTheFooter_When_ProductsAreSelectedAndAdded`
checks the screen flow. **Save and close** returns to the shop.

> Marking an In Progress Order with 3 lines ready makes it Ready to Send and it appears in the Unsent count as ready (S5)

`Should_BecomeReadyToSendAndKeepItsLines_When_AnInProgressOrderWithThreeLinesIsMarkedReady` marks a three-line order
and checks the state, the stored lines and that no request was made. `Should_CountReadyAndInProgressOrdersAndEveryItem_When_UnsentWorkIsSummarised`
and `Should_ShowTheUnsentCountWithWhatIsReady_When_WorkIsWaitingOnTheSyncScreen` check the count: "Unsent: 4 · 2 ready to send · 1 in progress".

> Marking an Order with 0 lines ready shows "Add at least one product" (S6)

`Should_RefuseWithAddAtLeastOneProductAndStayInProgress_When_TheOrderHasNoLines` checks both an order that never had a
line and a saved order with none. `Should_ShowAddAtLeastOneProductAndKeepEditing_When_MarkReadyToSendIsTappedOnAnEmptyOrder`
checks the message on screen, with the order still In Progress and still editable.

> Each Order records the Capturing Rep (glossary)

`Should_RecordCapturingRepAndShopWithNoCallLinked_When_TheFirstLineStartsAnOrder` checks that the signed-in rep is
stored, with the shop it is for and the shop it counts at.

## Scenario matrix and tests

All 25 agreed scenarios are covered. Rows 1 to 9 and 12 to 14 are in `OrderModelTests` and `OrderCaptureTests`; rows 20 to 24
in `OrderReadyTests`; rows 10 and 11 in `TabletOrderContractTests`; rows 15 to 19 in `NewOrderComponentTests`,
`LocationDetailComponentTests` and the existing reassignment-notice test. Row 25 holds because a real order is an
`Order`-kind Unsent item (asserted in `OrderModelTests` and `OrderReadyTests`), which the sign-out warning already counts.

The one existing assertion that changed is the `New Order` row of the "isn't available on this tablet yet" test in
`LocationDetailComponentTests`. It was replaced by the navigation test above, as approved. The `Record Call` row remains.

## Final verification

The complete Release solution suite passed on its first final run: **2,751 passed, zero failures, zero skips**, across
all 12 test projects. WI-037 recorded 2,703; the 48 new cases are 29 in Tablet Core integration, 16 in Tablet
Components and 3 in API unit tests.

| Project | Passed |
| --- | ---: |
| API integration | 143 |
| Demo data integration | 4 |
| Identity/admin integration | 804 |
| Snapshot spike integration | 22 |
| Tablet components integration | 44 |
| Tablet Core integration | 153 |
| Tablet spike integration | 41 |
| Web integration | 873 |
| API unit | 185 |
| Identity/admin unit | 256 |
| Tablet Core unit | 7 |
| Web unit | 219 |

Commands and artifacts (all under the git-ignored `.artifacts/wi038/`):

- `dotnet build FieldSales.slnx -c Release -m:1`: zero warnings, zero errors. Log: `build.log`.
- `dotnet test FieldSales.slnx -c Release --no-build -m:1 --settings sqlserver.runsettings --blame-hang-timeout 2m --logger trx --results-directory .artifacts/wi038/full-suite-results`:
  all projects passed, with Docker running. Log: `full-suite.log`.
- JavaScript and console tests: **38 passed**, none failed or skipped: tablet sign-out 4, Web product unit form 5,
  admin UI 18 (run from a mirrored scratch copy, so the repository stayed clean), and console status store 11.
- Demo script tests: **5 passed** in the cached `python:3.13-slim` container on LF-normalized copies, with Docker
  operations mocked. They cannot run natively on this Windows machine (Cygwin bash with Windows paths), and nothing
  this item changed is read by them.
- `scripts/tablet-app.ps1 -Action Build -Configuration Release`: signed Android publish succeeded with no warnings or
  errors. Log: `android-build.log`. APK SHA-256 `162208175925930ecf437a639a67e3313e200f715eec0be54ca030a4ffd6656e`
  (copy in `.artifacts/wi038/android/`). The APK's `app.css` matches the source byte for byte, and it contains the new
  `FieldSales.Ordering.Contracts` and `FieldSales.Quantities` assemblies beside the tablet ones.
- A mutation check removed the double-tap guard from the order screen; the double-tap test failed, and the guard was restored.
- `git diff --check` passed. A parsed comparison with the integration base confirms that only the `WI-038` status
  changed inside the delivery console.

## Not verified here

- **No physical tablet or browser run of the new screen.** The page's behaviour is covered by component tests that
  dispatch real Razor events, and the Android build compiles it, but its layout (the fixed footer, the result buttons,
  touch sizes) has not been looked at on a device or at 800x1280, 1280x800 and 360x640. That visual and device acceptance is the
  developer's to do.
- S4b (price source line) and S4c (Break Prompt), listed on the card as candidate test intents, belong to E6
  (WI-065, WI-066), as do S4d and S4e (WI-148, WI-155/156). They are intentionally not implemented here.

## Left for later items

- Reopen, edit, delete and remove a line (WI-046). Until then a wrong line, or an order left In Progress, stays on the tablet.
- Upload, and the Sent and Needs Attention states (WI-039); the Unsent list (WI-042); Home counts (WI-048).
- The Order Pad, measure steppers and unit display, the Low tab and the Review markers (WI-044, 045, 052, 054).
- Restricted-product permission (WI-043) and the full price engine (E6). The snapshot already leaves restricted products out.

Missing information: none for this item. Repository, native source paths and the `main` integration base come from the
approved predecessor. MI-62 and device administration remain outside the slice. WI-038 is `done`; the next item is
informational only and has not been started.
