using FieldSales.Calls.Contracts;

namespace FieldSales.Tablet.Core.Tests;

/// <summary>WI-056 agreed scenarios 2–4 (T-5.7.1, REP-US-016 S2): a Location's current stock level is the latest reading of each
/// product across its Calls, by the moment each Call was saved; every reading stays on its own Call.</summary>
[Trait("Category", "Unit")]
public sealed class StockReadingsTests
{
    private static readonly Guid Murphys = new("00000003-0000-0000-0000-00000000000a");
    private static readonly Guid ColdFlu = new("00000006-0000-0000-0000-000000000013");
    private static readonly Guid NasalSpray = new("00000006-0000-0000-0000-000000000015");

    private static TabletCall Call(DateTimeOffset savedAt, Guid? followUpOf, params CallStockEntry[] entries) =>
        new(CallContract.Schema, 1, Guid.NewGuid(), Murphys, "colm", DateOnly.FromDateTime(savedAt.DateTime), savedAt, CallChannel.InPerson,
            new("1.5", DateTimeOffset.UnixEpoch), null, entries, followUpOf);

    private static CallStockEntry Counted(Guid product, decimal? count, bool low = false) => new(product, "P", "Product", "Each", count, low);

    // Scenario 2 (S2): 3 at 09:40, then a follow-up's 13 at 16:00.
    [Fact]
    public void Should_Make13TheCurrentStockLevelAndKeepBothReadings_When_AFollowUpAt1600Counts13OverThe0940CountOf3()
    {
        TabletCall original = Call(new(2026, 9, 22, 9, 40, 0, TimeSpan.FromHours(1)), null, Counted(ColdFlu, 3m));
        TabletCall followUp = Call(new(2026, 9, 22, 16, 0, 0, TimeSpan.FromHours(1)), original.Id, Counted(ColdFlu, 13m), Counted(NasalSpray, 2m));

        IReadOnlyDictionary<Guid, StockReading> current = StockReadings.Current(Murphys, [original, followUp]);

        Assert.Equal((13m, followUp.Id), (current[ColdFlu].Count, current[ColdFlu].CallId));
        Assert.Equal((2m, followUp.Id), (current[NasalSpray].Count, current[NasalSpray].CallId));
        Assert.Equal(3m, original.StockEntries!.Single().Count); // The earlier reading stays on its Call.
    }

    // Scenario 3: the checkpoint question. Listed (or uploaded) out of order, and saved in different offsets, the later save wins.
    [Fact]
    public void Should_PickTheLatestSavedReading_When_CallsAreRecordedOutOfOrder()
    {
        // 09:10 on a tablet in Irish summer time is 08:10Z, so the Call saved at 08:30 on a tablet set to UTC is the later one,
        // although its clock reads earlier.
        TabletCall later = Call(new(2026, 9, 22, 8, 30, 0, TimeSpan.Zero), null, Counted(ColdFlu, 7m));
        TabletCall earlier = Call(new(2026, 9, 22, 9, 10, 0, TimeSpan.FromHours(1)), null, Counted(ColdFlu, 4m));
        // A follow-up to last week's Call, saved yesterday evening: its date is later than last week's, but it was saved before both.
        TabletCall yesterday = Call(new(2026, 9, 21, 19, 0, 0, TimeSpan.FromHours(1)), Guid.NewGuid(), Counted(ColdFlu, 30m));

        Assert.Equal(7m, StockReadings.Current(Murphys, [later, yesterday, earlier])[ColdFlu].Count);
        Assert.Equal(7m, StockReadings.Current(Murphys, [earlier, later, yesterday])[ColdFlu].Count);
    }

    // Scenario 4 (D4): only counts are readings; Out (a count of 0) is one.
    [Fact]
    public void Should_IgnoreNotCheckedAndLowOnlyEntries_When_WorkingOutTheCurrentStockLevel()
    {
        TabletCall counted = Call(new(2026, 9, 22, 9, 40, 0, TimeSpan.Zero), null, Counted(ColdFlu, 3m), Counted(NasalSpray, 5m));
        TabletCall later = Call(new(2026, 9, 22, 16, 0, 0, TimeSpan.Zero), counted.Id,
            Counted(ColdFlu, null), Counted(NasalSpray, null, low: true));
        TabletCall latest = Call(new(2026, 9, 22, 17, 0, 0, TimeSpan.Zero), counted.Id, Counted(NasalSpray, 0m, low: true));

        IReadOnlyDictionary<Guid, StockReading> current = StockReadings.Current(Murphys, [counted, later]);
        Assert.Equal(3m, current[ColdFlu].Count); // Not checked later on says nothing about the level.
        Assert.Equal(5m, current[NasalSpray].Count); // Nor does a Low mark without a count.
        Assert.Equal(0m, StockReadings.Current(Murphys, [counted, later, latest])[NasalSpray].Count); // Out is a reading.
        Assert.Empty(StockReadings.Current(Guid.NewGuid(), [counted, later, latest])); // Another Location's Calls don't count.
    }
}
