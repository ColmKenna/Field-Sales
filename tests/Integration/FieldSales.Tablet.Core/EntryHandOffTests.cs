using Microsoft.Extensions.Time.Testing;

namespace FieldSales.Tablet.Core.Tests;

// WI-032 startup: a cold start passes the entry check's result to the next screen so the 24-hour rule runs once.
// Anything stale, repeated or missing means the screen checks for itself, so the rule is never skipped.
public sealed class EntryHandOffTests
{
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(5);
    private static readonly TabletEntry Home = new(EntryScreen.Home, "colm", "");
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 1, 6, 42, 0, TimeSpan.Zero));

    [Fact]
    public void Should_GiveTheOfferedEntryToTheNextScreen_When_ItArrivesStraightAway()
    {
        EntryHandOff handOff = new(_clock, Window);
        handOff.Offer(Home);
        _clock.Advance(TimeSpan.FromMilliseconds(300));

        Assert.Same(Home, handOff.Take());
    }

    [Fact]
    public void Should_GiveItOnlyOnce_When_TwoScreensAsk()
    {
        EntryHandOff handOff = new(_clock, Window);
        handOff.Offer(Home);

        Assert.Same(Home, handOff.Take());
        Assert.Null(handOff.Take());
    }

    [Fact]
    public void Should_MakeTheScreenCheckAgain_When_TheOfferIsStale()
    {
        EntryHandOff handOff = new(_clock, Window);
        handOff.Offer(Home);
        _clock.Advance(Window + TimeSpan.FromMilliseconds(1));

        Assert.Null(handOff.Take());
    }

    [Fact]
    public void Should_NotBeFooledByTheWallClock_When_TheDateIsMovedAfterTheOffer()
    {
        TimerClock clock = new();
        EntryHandOff handOff = new(clock, Window);
        handOff.Offer(Home);
        // The wall clock jumps two days; only elapsed time on the device's own timer counts.
        clock.WallClock = clock.WallClock.AddDays(2);

        Assert.Same(Home, handOff.Take());
    }

    /// <summary>A wall clock that can jump while the device timer (milliseconds here) stands still.</summary>
    private sealed class TimerClock : TimeProvider
    {
        public DateTimeOffset WallClock { get; set; } = new(2026, 10, 1, 6, 42, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => WallClock;
        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() => 0;
    }

    [Fact]
    public void Should_GiveNothing_When_NothingWasOffered()
    {
        Assert.Null(new EntryHandOff(_clock, Window).Take());
    }

    [Fact]
    public void Should_GiveTheNewestOffer_When_OneReplacesAnother()
    {
        EntryHandOff handOff = new(_clock, Window);
        TabletEntry sync = new(EntryScreen.Sync, "colm", "");
        handOff.Offer(Home);
        handOff.Offer(sync);

        Assert.Same(sync, handOff.Take());
    }
}
