using FieldSales.Ordering.Contracts;
using FieldSales.Pricing;
using FieldSales.Snapshot.Contracts;
using Microsoft.Extensions.Time.Testing;

namespace FieldSales.Tablet.Core.Tests;

/// <summary>WI-059 (T-1.3.2, PRD-US-002): a future-dated price applies on the tablet on its day, by the business day in Ireland
/// (D1), never before the download day (D2), with no second Sync.</summary>
[Trait("Category", "Integration")]
public sealed class FutureDatedPriceTests : IDisposable
{
    private static readonly Guid Category = Guid.Parse("20000000-0000-0000-0000-000000000059");
    private static readonly Guid VitaminD = Guid.Parse("30000000-0000-0000-0000-000000000059");
    private static readonly Guid VitaminC = Guid.Parse("30000000-0000-0000-0000-000000000060");
    private static readonly TimeZoneInfo NewYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    private readonly TestTablet tablet = new();
    public void Dispose() => tablet.Dispose();

    // A snapshot built on builtOn carrying the price in effect then and every later one, as the server sends it (WI-032).
    private async Task<Guid> SyncAsync(DateOnly builtOn, params SnapshotBasePrice[] prices)
    {
        TabletSnapshot snapshot = Snapshots.Build("colm", 1, 0);
        SnapshotCategory[] categories = [new(Category, null, "Vitamins")];
        SnapshotProduct[] products = [new(VitaminD, "VITD-1000-90", "Vitamin D 1000IU 90s", Category, "Each", null, null, null),
            new(VitaminC, "VITC-500", "Vitamin C 500mg", Category, "Each", null, null, null)];
        snapshot = snapshot with
        {
            Categories = categories, Products = products, BasePrices = prices,
            Manifest = snapshot.Manifest with
            {
                BuiltUtc = new DateTimeOffset(builtOn, new TimeOnly(5, 30), TimeSpan.Zero), BuildDate = builtOn,
                Counts = new(1, categories.Length, products.Length, prices.Length)
            }
        };
        await tablet.SyncAsync(snapshot);
        return snapshot.Locations[0].Id;
    }

    private static SnapshotBasePrice Price(int month, int day, decimal amount, Guid? product = null) =>
        new(product ?? VitaminD, new DateOnly(2026, month, day), amount);

    // A tablet clock at a UTC instant, set to a time zone of its own.
    private static FakeTimeProvider ClockAt(DateTimeOffset utc, TimeZoneInfo? zone = null)
    {
        FakeTimeProvider clock = new(utc);
        clock.SetLocalTimeZone(zone ?? TimeZoneInfo.Utc);
        return clock;
    }

    // The product index as Order entry reads it: TabletApp asks for the business day by the tablet's clock.
    private decimal? IndexPrice(TimeProvider clock) => Assert.Single(new SnapshotStore(tablet.Workspace(), clock)
        .ReadProductIndex(BusinessCalendar.Today(clock.GetUtcNow())).Search("vitamin d").Results).Price;

    private decimal LinePrice(TimeProvider clock, Guid shop)
    {
        OrderResult added = new OrderStore(tablet.Workspace(), clock).AddLine(Guid.NewGuid(), shop, VitaminD, 12);
        Assert.Null(added.Error);
        return Assert.Single(added.Order!.Lines).Price.Amount;
    }

    [Fact]
    public async Task Should_Quote1250On31OctAnd1320On1Nov_When_SyncedOn31OctBeforeTheRise()
    {
        Guid shop = await SyncAsync(new(2026, 10, 31), Price(10, 1, 12.50m), Price(11, 1, 13.20m));

        FakeTimeProvider clock = ClockAt(new(2026, 10, 31, 10, 0, 0, TimeSpan.Zero));
        Assert.Equal((12.50m, 12.50m), (IndexPrice(clock), LinePrice(clock, shop)));

        clock.Advance(TimeSpan.FromHours(23)); // 09:00 on 1 Nov, with no second Sync.
        Assert.Equal((13.20m, 13.20m), (IndexPrice(clock), LinePrice(clock, shop)));
    }

    [Fact]
    public async Task Should_SwitchAtMidnightIrishTime_When_TheTabletIsSetToAnotherTimeZone()
    {
        // A winter rise: Ireland is on GMT, and a New York tablet still reads 31 Oct for hours after Irish midnight.
        Guid shop = await SyncAsync(new(2026, 10, 31), Price(10, 1, 12.50m), Price(11, 1, 13.20m));
        FakeTimeProvider newYork = ClockAt(new(2026, 10, 31, 23, 59, 0, TimeSpan.Zero), NewYork);
        Assert.Equal((12.50m, 12.50m), (IndexPrice(newYork), LinePrice(newYork, shop)));
        newYork.Advance(TimeSpan.FromMinutes(2)); // 00:01 in Dublin, 20:01 on 31 Oct in New York.
        Assert.Equal((13.20m, 13.20m), (IndexPrice(newYork), LinePrice(newYork, shop)));

        // A summer rise: Irish Standard Time is UTC+1, so a UTC tablet reaches 1 Oct an hour after Ireland does.
        Guid summerShop = await SyncAsync(new(2026, 9, 30), Price(9, 1, 11.80m), Price(10, 1, 12.50m));
        FakeTimeProvider utc = ClockAt(new(2026, 9, 30, 22, 59, 0, TimeSpan.Zero));
        Assert.Equal((11.80m, 11.80m), (IndexPrice(utc), LinePrice(utc, summerShop)));
        utc.Advance(TimeSpan.FromMinutes(2)); // 00:01 on 1 Oct in Dublin, 23:01 on 30 Sep in UTC.
        Assert.Equal((12.50m, 12.50m), (IndexPrice(utc), LinePrice(utc, summerShop)));
    }

    [Fact]
    public async Task Should_NeverQuoteBeforeTheDownloadDay_When_TheTabletClockIsSetBack()
    {
        // The €12.50 in effect on the download day started on 25 Oct; by a clock set back to 20 Oct no price would have started yet.
        Guid shop = await SyncAsync(new(2026, 10, 31), Price(10, 25, 12.50m), Price(11, 1, 13.20m));

        FakeTimeProvider setBack = ClockAt(new(2026, 10, 20, 10, 0, 0, TimeSpan.Zero));

        Assert.Equal((12.50m, 12.50m), (IndexPrice(setBack), LinePrice(setBack, shop)));
    }

    [Fact]
    public async Task Should_KeepTheLineAt1250AndPriceANewLineAt1320_When_AnOrderIsEditedAfterMidnight()
    {
        Guid shop = await SyncAsync(new(2026, 10, 31), Price(10, 1, 12.50m), Price(11, 1, 13.20m),
            Price(10, 1, 12.50m, VitaminC), Price(11, 1, 13.20m, VitaminC));
        FakeTimeProvider clock = ClockAt(new(2026, 10, 31, 23, 58, 0, TimeSpan.Zero));
        OrderStore orders = new(tablet.Workspace(), clock);
        Guid orderId = Guid.NewGuid();
        OrderLine vitaminD = Assert.Single(orders.AddLine(orderId, shop, VitaminD, 12).Order!.Lines);
        Assert.Equal(12.50m, vitaminD.Price.Amount);

        clock.Advance(TimeSpan.FromMinutes(4)); // 00:02 on 1 Nov, when both are €13.20.
        OrderResult changed = orders.ChangeQuantity(orderId, vitaminD.Id, 24);
        OrderResult added = orders.AddLine(orderId, shop, VitaminC, 6);

        Assert.Null(changed.Error);
        Assert.Equal((24m, 12.50m), (changed.Order!.Lines.Single(line => line.ProductId == VitaminD).Quantity,
            changed.Order.Lines.Single(line => line.ProductId == VitaminD).Price.Amount));
        Assert.Null(added.Error);
        Assert.Equal([(VitaminD, 24m, 12.50m), (VitaminC, 6m, 13.20m)],
            added.Order!.Lines.Select(line => (line.ProductId, line.Quantity, line.Price.Amount)));
    }
}
