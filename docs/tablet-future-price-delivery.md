# WI-059 — Apply a future-dated price on the tablet on its day

T-1.3.2 / PRD-US-002 S1 (tablet part), S2. Feature branch: `feature/wi-059-apply-future-dated-price-tablet`.

Head office adds a price with the date it applies from (T-1.3.1). The Morning Snapshot already carries every future-dated price
(WI-032), and the tablet already chose the newest price on or before "today". This item settles which day "today" is. It is the
date in Ireland, on the tablet and on the server alike, and never a day before the snapshot was downloaded. It also keeps the
prices on an open page correct across midnight. A line keeps the price it was captured at.

## Decisions

The developer answered four questions and replied **Approved** to D1–D8, the steps and the 7 scenarios. They then said "proceed",
which was taken as **Apply**. The item is Agent-Assisted (Checkpointed): the developer reviewed scenarios 1–3 at the checkpoint and
replied `Continue T-1.3.2`.

| # | Decision |
|---|---|
| D1 | **Business day (MI-53).** The price day is the date in Europe/Dublin, whatever time zone the tablet is set to. `BusinessCalendar.Today(now)` in `FieldSales.Pricing` is shared by the tablet and the server; the server's `ProductEndpoints.Today` now calls it. Call and order dates are unchanged: an order's `TakenOn` is still the tablet's own day. |
| D2 | **Clock.** The tablet's clock is trusted, but prices are never read for a day before the snapshot's build date (`BusinessCalendar.PriceDay`). A clock set back can't quote prices older than the download. A clock set forward can't be detected offline. |
| D3 | **Where it applies.** Every price the tablet reads: the product index (Order entry, the stock check's catalogue) and adding a line. The build-date floor is applied in `SnapshotStore`, in the same transaction as the prices. |
| D4 | **Midnight.** `PriceDayWatch` (Tablet.Core) checks the business day once a minute while the app is in the foreground. When it changes, `TabletApp.PriceDayChanged` is raised, and every `OfflineCataloguePage` reads again as it does on resume, keeping the order. A line added in the minute after midnight is already priced on the new day, because adding a line works out the day itself. |
| D5 | **Captured lines.** A line keeps the price it was added at; changing its quantity never re-prices it. A line added after midnight gets the new price. The server stores lines as received. |
| D6 | **Engine input.** The tablet keeps its "newest price on or before the day" read, now given the price day. The engine (WI-058) is wired into lines by WI-064 and WI-065 (inferred). |
| D7 | **Out of scope.** Tiers and breaks in the snapshot (T-6.4.2), price display (T-6.4.3), head office price editing (T-1.3.1, done), order and call dates, detecting a wrong clock at Sync. |
| D8 | **Finish.** Push, PR, merge after CI (merge commit, branch kept), update local main. |

## Criteria

| Criterion | How it is met |
|---|---|
| 1. The snapshot carries each product's current base price and every future-dated price with its Effective From date | Already met by WI-032: `TabletSnapshotApiTests.Should_IncludeTodaysAndFutureBasePrices_When_PricesAreDated` (unchanged, passing) |
| 2. A tablet synced on 31 October resolves €12.50 on 31 October and €13.20 on 1 November without another Sync | Scenarios 1, 2, 3, 5, 6 |
| 3. A line captured on 28 October at €12.50 still shows €12.50 after 1 November, on the tablet and on the server | Scenarios 4, 7 |

## Agreed scenarios

| # | Scenario | Where |
|---|---|---|
| 1 | `Should_Quote1250On31OctAnd1320On1Nov_When_SyncedOn31OctBeforeTheRise` | `tests/Integration/FieldSales.Tablet.Core/FutureDatedPriceTests.cs` |
| 2 | `Should_SwitchAtMidnightIrishTime_When_TheTabletIsSetToAnotherTimeZone`: a New York tablet for the 1 Nov rise (GMT), a UTC tablet for a 1 Oct rise (Irish summer time) | same |
| 3 | `Should_NeverQuoteBeforeTheDownloadDay_When_TheTabletClockIsSetBack` | same |
| 4 | `Should_KeepTheLineAt1250AndPriceANewLineAt1320_When_AnOrderIsEditedAfterMidnight` | same |
| 5 | `Should_RaiseThePriceDayChangeOnce_When_MidnightPassesInDublin` | `tests/Unit/FieldSales.Tablet.Core/PriceDayWatchTests.cs` |
| 6 | `Should_ShowTodaysPrices_When_OrderEntryIsLeftOpenOverMidnight` | `tests/Integration/FieldSales.Tablet.Components/NewOrderComponentTests.PriceDay.cs` |
| 7 | `Should_KeepThe1250LineOnTheServer_When_AnOrderCapturedOn28OctSyncsOn2Nov` | `tests/Integration/FieldSales.Api/TabletUploadEndToEndTests.cs` |

Scenario 7 needed the API test fixture's clock to move, so `TabletSnapshotApplication.Clock` is now settable. Each reset puts it
back to its old fixed time, 30 September 23:30 UTC.

Mutation checks, each run against the whole test project:

| Mutation | Tests that failed |
|---|---|
| A line priced by the tablet's own day | 2 |
| No build-date floor | 3 |
| The business day taken as the UTC date | 2 |
| The watch raises on every check, not only on a change | 5 |
| Pages ignore the day change | 6 |
| A quantity change re-prices the line | 4 |

## Verification

- `dotnet build FieldSales.slnx -c Release --no-incremental`: no warnings.
- Full suite (SQL Server): 3,339 passed, 0 failed (WI-058 ended at 3,332; plus the 7 scenarios).
- `node --test plan_docs/field-sales-delivery/tests/status-store.test.js`: 11 passed.
- Android Release build (`scripts/tablet-app.ps1 -Action Build -Configuration Release`): succeeded.

## Not done here

- **Prices in the product list.** The product-list half of scenarios 1–3 works out the day in the test, using the same function
  `TabletApp` calls. The one-line `TabletApp.PriceToday` itself is covered only by the Android build; the screen test runs against
  the desktop adapter. Line prices go through the real `OrderStore`.
- **No device check.** On Android, Europe/Dublin depends on the device's time-zone data. .NET supports it there, but it hasn't been
  checked on a tablet.
- **Up to a minute late.** Prices on an open page may change up to a minute after midnight.
