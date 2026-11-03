using FieldSales.Calls.Contracts;
using FieldSales.Ordering.Contracts;
using FieldSales.Snapshot.Contracts;
using Microsoft.Extensions.Time.Testing;

namespace FieldSales.Tablet.Core.Tests;

public sealed partial class HomeRecordTests
{
    [Fact]
    public async Task Should_ShowMurphysDoneAndNolansUnplannedWithTheirOrderStates_When_TheS2DayIsRead()
    {
        using DayFixture day = await DayFixture.Create();
        day.CaptureS2();
        HomeDayRecord record = day.Reader.Read([day.Murphys]);
        Assert.Equal(new DateOnly(2026, 9, 22), record.Day);
        HomeDayLocation murphys = Assert.Single(record.Sections[0].Locations);
        Assert.Equal(day.Murphys, murphys.Location);
        Assert.True(murphys.Done);
        Assert.True(murphys.IsPlanned);
        Assert.Equal("09:40", Assert.Single(murphys.Calls).SavedAt.ToString("HH:mm"));
        Assert.Equal("Ready to Send", Assert.Single(murphys.Orders).State);
        HomeDayLocation nolans = Assert.Single(record.Sections[1].Locations);
        Assert.Equal(day.Nolans, nolans.Location);
        Assert.False(nolans.IsPlanned);
        Assert.False(nolans.Done);
        Assert.Equal("11:15", Assert.Single(nolans.Orders).SavedAt.ToString("HH:mm"));
        Assert.Equal(3, Assert.Single(record.Counters).Count);
    }

    [Fact]
    public async Task Should_KeepActivityAndTheGlobalCount_When_NoVisitPlanExists()
    {
        using DayFixture day = await DayFixture.Create();
        day.CaptureS2();
        HomeDayRecord record = day.Reader.Read();
        Assert.Empty(record.Sections[0].Locations);
        Assert.Equal("No visits scheduled today", record.Sections[0].EmptyText);
        Assert.Equal(2, record.Sections[1].Locations.Count);
        Assert.All(record.Sections[1].Locations, row => Assert.False(row.IsPlanned));
        Assert.True(record.Sections[1].Locations.Single(row => row.Location.Id == day.Murphys.Id).Done);
    }

    [Fact]
    public async Task Should_KeepTheSuppliedPlanPositions_When_AVisitIsDone()
    {
        using DayFixture day = await DayFixture.Create();
        day.CaptureS2();
        var record = day.Reader.Read([day.Nolans, day.Murphys]);
        Assert.Equal([day.Nolans.Id, day.Murphys.Id], record.Sections[0].Locations.Select(row => row.Location.Id));
        Assert.True(record.Sections[0].Locations[1].Done);
        Assert.Empty(record.Sections[1].Locations);
    }

    [Fact]
    public async Task Should_KeepTodayAfterConfirmationAndRemoveOnlyTheUnsentCount_When_WorkBecomesSent()
    {
        using DayFixture day = await DayFixture.Create();
        day.CaptureS2();
        var before = day.Reader.Read();
        foreach (var item in day.Store.Read()) Assert.True(day.Store.MarkSent(item.Id, day.Clock.GetUtcNow()));
        var after = day.Reader.Read();
        Assert.Equal(0, Assert.Single(after.Counters).Count);
        Assert.Equal(2, after.Sections[1].Locations.Count);
        Assert.Equal(before.Sections[1].Locations.Sum(row => row.Calls.Count), after.Sections[1].Locations.Sum(row => row.Calls.Count));
        Assert.All(after.Sections[1].Locations.SelectMany(row => row.Orders), order => Assert.Equal(OrderStanding.Sent, order.Standing));
    }

    [Fact]
    public async Task Should_UseTheBusinessDateAndLatestSavedTime_When_AnOrderIsEditedOnAnotherDay()
    {
        using DayFixture day = await DayFixture.Create();
        day.CaptureS2();
        day.Clock.Advance(TimeSpan.FromDays(1));
        Assert.True(day.Orders.Reopen(day.NolanOrder).Succeeded);
        var changed = day.Orders.AddLine(day.NolanOrder, day.Nolans.Id, day.Snapshot.Products[0].Id, 1);
        Assert.True(changed.Succeeded, changed.Error);
        Assert.Empty(day.Reader.Read().Sections[1].Locations);
        Assert.Equal(3, Assert.Single(day.Reader.Read().Counters).Count); // The queue is not filtered to today.
    }

    [Fact]
    public async Task Should_ShowTheCurrentStandingWithoutChangingSavedWork_When_OrdersAreClaimedRefusedOrReopened()
    {
        using DayFixture day = await DayFixture.Create();
        day.CaptureS2();
        Assert.True(day.Orders.Reopen(day.MurphyOrder).Succeeded);
        Assert.Equal("In Progress", Murphy().State);
        Assert.Equal(day.Clock.GetUtcNow(), Murphy().SavedAt);
        Assert.True(day.Orders.MarkReadyToSend(day.MurphyOrder).Succeeded);
        Assert.True(day.Store.Claim(day.Store.Find(day.MurphyOrder)!, day.Clock.GetUtcNow()));
        Assert.Equal("Sending", Murphy().State);
        Assert.True(day.Store.MarkNeedsAttention(day.Store.Find(day.MurphyOrder)!, new("incomplete", "private fault", 400, day.Clock.GetUtcNow())));
        string body = day.Store.Fingerprint();
        Assert.Equal("Needs Attention", Murphy().State);
        Assert.Equal("1 needs attention", Assert.Single(day.Reader.Read().Counters).Detail);
        Assert.Equal(body, day.Store.Fingerprint());
        HomeOrder Murphy() => day.Reader.Read().Sections[1].Locations.Single(row => row.Location.Id == day.Murphys.Id).Orders.Single();
    }

    [Fact]
    public async Task Should_ShowAllRecordsOnceAndLeaveTheDatabasesUntouched_When_TheDayIsReadRepeatedly()
    {
        using DayFixture day = await DayFixture.Create();
        day.CaptureS2();
        Assert.True(day.Calls.Save(Guid.NewGuid(), day.Murphys.Id, new(CallChannel.Phone, "Follow-up pitch")).Succeeded);
        Assert.True(day.Orders.AddLine(Guid.NewGuid(), day.Murphys.Id, day.Snapshot.Products[0].Id, 1).Succeeded);
        string fingerprint = day.Store.Fingerprint();
        string fileHash = day.Tablet.UnsentFileHash();
        var record = day.Reader.Read();
        var row = record.Sections[1].Locations.Single(row => row.Location.Id == day.Murphys.Id);
        Assert.Equal(2, row.Calls.Count);
        Assert.Equal(2, row.Orders.Count);
        Assert.Equal(fingerprint, day.Store.Fingerprint());
        Assert.Equal(fileHash, day.Tablet.UnsentFileHash());
    }

    [Fact]
    public async Task Should_KeepTheCountAndUsableActivity_When_LegacyWorkCannotBeRead()
    {
        using DayFixture day = await DayFixture.Create();
        day.CaptureS2();
        day.Store.Save(new(Guid.NewGuid(), UnsentKind.Call, day.Clock.GetUtcNow(), """{"old":"call"}"""));
        var result = day.Reader.Read();
        Assert.True(result.HasUnreadableActivity);
        Assert.Equal(4, Assert.Single(result.Counters).Count);
        Assert.Equal(2, result.Sections[1].Locations.Count);
    }

    [Fact]
    public async Task Should_ShowNoOtherRepsActivity_When_AnotherWorkspaceIsRead()
    {
        using DayFixture day = await DayFixture.Create();
        day.CaptureS2();
        var other = new HomeRecordReader(day.Tablet.Workspace("aoife"), day.Clock).Read();
        Assert.All(other.Sections, section => Assert.Empty(section.Locations));
        Assert.Equal(0, Assert.Single(other.Counters).Count);
        Assert.False(File.Exists(day.Tablet.Workspace("aoife").UnsentPath));
    }

    private sealed class DayFixture : IDisposable
    {
        public TestTablet Tablet { get; } = new();
        public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 9, 22, 9, 40, 0, TimeSpan.Zero));
        public TabletSnapshot Snapshot { get; private set; } = null!;
        public HomeLocation Murphys => new(Snapshot.Locations[0].Id, "Murphy's Pharmacy", "Rathdrum", "Murphy's");
        public HomeLocation Nolans => new(Snapshot.Locations[1].Id, "Nolan's Pharmacy", "Rathdrum", "Nolan's");
        public Guid MurphyOrder { get; } = Guid.NewGuid();
        public Guid NolanOrder { get; } = Guid.NewGuid();
        public CallStore Calls => new(Tablet.Workspace(), Clock);
        public OrderStore Orders => new(Tablet.Workspace(), Clock);
        public UnsentStore Store => new(Tablet.Workspace());
        public HomeRecordReader Reader => new(Tablet.Workspace(), Clock);
        public static async Task<DayFixture> Create()
        {
            var fixture = new DayFixture();
            var s = Snapshots.Build("colm", 2, 1);
            fixture.Snapshot = s with
            {
                Locations = [s.Locations[0] with { Name = "Murphy's Pharmacy", CustomerName = "Murphy's", Town = s.Locations[0].Town with { Name = "Rathdrum" } },
                    s.Locations[1] with { Name = "Nolan's Pharmacy", CustomerName = "Nolan's", Town = s.Locations[1].Town with { Name = "Rathdrum" } }],
                BasePrices = s.BasePrices.Select(p => p with { EffectiveFrom = p.EffectiveFrom.AddMonths(-1) }).ToArray()
            };
            await fixture.Tablet.SyncAsync(fixture.Snapshot);
            return fixture;
        }
        public void CaptureS2()
        {
            Assert.True(Calls.Save(Guid.NewGuid(), Murphys.Id, new(CallChannel.InPerson, "Presented autumn range")).Succeeded);
            Assert.True(Orders.AddLine(MurphyOrder, Murphys.Id, Snapshot.Products[0].Id, 6).Succeeded);
            Assert.True(Orders.MarkReadyToSend(MurphyOrder).Succeeded);
            Clock.Advance(TimeSpan.FromMinutes(95));
            Assert.True(Orders.AddLine(NolanOrder, Nolans.Id, Snapshot.Products[0].Id, 3).Succeeded);
            Assert.True(Orders.MarkReadyToSend(NolanOrder).Succeeded);
        }
        public void Dispose() => Tablet.Dispose();
    }
}
