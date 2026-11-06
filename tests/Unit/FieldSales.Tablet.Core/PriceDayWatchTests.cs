using Microsoft.Extensions.Time.Testing;

namespace FieldSales.Tablet.Core.Tests;

/// <summary>WI-059 agreed scenario 5 (D4): open screens hear once when midnight passes in Ireland.</summary>
[Trait("Category", "Unit")]
public sealed class PriceDayWatchTests
{
    [Fact]
    public void Should_RaiseThePriceDayChangeOnce_When_MidnightPassesInDublin()
    {
        // 20:00 on 31 Oct in Dublin (GMT), on a tablet set to New York, where midnight comes five hours later.
        FakeTimeProvider clock = new(new DateTimeOffset(2026, 10, 31, 20, 0, 0, TimeSpan.Zero));
        clock.SetLocalTimeZone(TimeZoneInfo.FindSystemTimeZoneById("America/New_York"));
        int raised = 0;
        using PriceDayWatch watch = new(clock, () => raised++);
        watch.Start();

        clock.Advance(TimeSpan.FromHours(1));
        Assert.Equal(0, raised); // Within the day.

        clock.SetUtcNow(new DateTimeOffset(2026, 10, 31, 23, 50, 0, TimeSpan.Zero));
        clock.Advance(PriceDayWatch.Interval);
        Assert.Equal(0, raised); // The clock moved on, but it's still 31 Oct in Dublin.

        clock.Advance(TimeSpan.FromMinutes(10)); // 00:01 on 1 Nov in Dublin; 20:01 on 31 Oct in New York.
        Assert.Equal(1, raised);

        clock.Advance(TimeSpan.FromHours(6)); // Past New York's midnight: the business day hasn't changed again.
        Assert.Equal(1, raised);
    }
}
