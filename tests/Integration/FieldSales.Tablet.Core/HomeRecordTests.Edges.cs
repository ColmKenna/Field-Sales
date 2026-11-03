using FieldSales.Calls.Contracts;

namespace FieldSales.Tablet.Core.Tests;

public sealed partial class HomeRecordTests
{
    [Fact]
    public async Task Should_UseTheTabletLocalBusinessDayAcrossMidnight_When_UtcIsStillOnThePreviousDate()
    {
        using DayFixture day = await DayFixture.Create();
        day.Clock.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("Fixture +14", TimeSpan.FromHours(14), "Fixture +14", "Fixture +14"));
        var call = day.Calls.Save(Guid.NewGuid(), day.Murphys.Id, new(CallChannel.Phone, "Late call"));
        Assert.True(call.Succeeded, call.Error);
        Assert.True(day.Orders.AddLine(Guid.NewGuid(), day.Murphys.Id, day.Snapshot.Products[0].Id, 1).Succeeded);
        var before = day.Reader.Read();
        Assert.Equal(new DateOnly(2026, 9, 22), before.Day);
        Assert.Equal("23:40", Assert.Single(before.Sections[1].Locations).Calls.Single().SavedAt.ToString("HH:mm"));
        day.Clock.Advance(TimeSpan.FromMinutes(30));
        Assert.Empty(day.Reader.Read().Sections[1].Locations);
        Assert.True(day.Calls.Save(Guid.NewGuid(), day.Nolans.Id, new(CallChannel.Phone, "After midnight")).Succeeded);
        var order = day.Orders.AddLine(Guid.NewGuid(), day.Nolans.Id, day.Snapshot.Products[0].Id, 1);
        Assert.True(order.Succeeded, order.Error);
        var after = day.Reader.Read();
        Assert.Equal(new DateOnly(2026, 9, 23), after.Day);
        Assert.Equal(new DateOnly(2026, 9, 22), DateOnly.FromDateTime(day.Clock.GetUtcNow().DateTime));
        var row = Assert.Single(after.Sections[1].Locations);
        Assert.Equal(day.Nolans.Id, row.Location.Id);
        Assert.Equal("00:10", Assert.Single(row.Calls).SavedAt.ToString("HH:mm"));
        Assert.Equal("00:10", Assert.Single(row.Orders).SavedAt.ToString("HH:mm"));
        Assert.Equal(4, Assert.Single(after.Counters).Count);
    }

    [Fact]
    public async Task Should_ShowAnEmptyDayInsteadOfTreatingAssignmentsAsVisits_When_NoActivityWasSaved()
    {
        using DayFixture day = await DayFixture.Create();
        var record = day.Reader.Read();
        Assert.Equal(0, Assert.Single(record.Counters).Count);
        Assert.All(record.Sections, section => Assert.Empty(section.Locations));
        Assert.False(record.HasUnreadableActivity);
        Assert.False(File.Exists(day.Tablet.Workspace().UnsentPath));
    }

    [Fact]
    public async Task Should_KeepSentActivityWithAnExplicitMissingIdentity_When_AReassignedShopLeavesTheSnapshot()
    {
        using DayFixture day = await DayFixture.Create();
        day.CaptureS2();
        foreach (var item in day.Store.Read().Where(item => item.Kind == UnsentKind.Call || item.Id == day.MurphyOrder))
            Assert.True(day.Store.MarkSent(item.Id, day.Clock.GetUtcNow()));
        var replacement = day.Snapshot with
        {
            Locations = [day.Snapshot.Locations[1]],
            Manifest = day.Snapshot.Manifest with { Counts = day.Snapshot.Manifest.Counts with { Locations = 1 } }
        };
        var encoded = EncodedSnapshot.From(replacement);
        string gzip = Path.Combine(day.Tablet.Root, "replacement.gz");
        await File.WriteAllBytesAsync(gzip, encoded.Gzip);
        await new SnapshotStore(day.Tablet.Workspace(), day.Clock).ApplyAsync(gzip, encoded.Expected);
        var row = day.Reader.Read().Sections[1].Locations.Single(row => row.Location.Id == day.Murphys.Id);
        Assert.Equal("Shop details unavailable", row.Location.Name);
        Assert.True(row.Done);
        Assert.Single(row.Calls);
        Assert.Equal("Sent", Assert.Single(row.Orders).State);
        Assert.Equal(1, Assert.Single(day.Reader.Read().Counters).Count);
    }
}
