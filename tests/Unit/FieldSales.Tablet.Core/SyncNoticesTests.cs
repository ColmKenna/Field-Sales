namespace FieldSales.Tablet.Core.Tests;

// WI-040, found on the Samsung: returning from the sign-in browser rebuilds the Sync page, so what it last told the rep is kept
// by the app for that rep and session.
[Trait("Category", "Unit")]
public sealed class SyncNoticesTests
{
    private static readonly SyncNotice SignIn = new(new SyncReport("Sign in again to send 2 items", null), SignInNeeded: true);

    [Fact]
    public void Should_GiveTheNoticeBack_When_TheSameRepAsksInTheSameSession()
    {
        SyncNotices notices = new();
        notices.Keep("colm", 4, SignIn);

        Assert.Same(SignIn, notices.Read("colm", 4));
    }

    [Theory]
    [InlineData("colm", 5)]
    [InlineData("aoife", 4)]
    public void Should_GiveNothing_When_AnotherSessionOrRepAsks(string rep, long version)
    {
        SyncNotices notices = new();
        notices.Keep("colm", 4, SignIn);

        Assert.Null(notices.Read(rep, version));
    }

    [Fact]
    public void Should_ForgetIt_When_NothingIsKept()
    {
        SyncNotices notices = new();
        notices.Keep("colm", 4, SignIn);

        notices.Keep("colm", 4, null);

        Assert.Null(notices.Read("colm", 4));
    }

    [Fact]
    public void Should_TellTheRebuiltPage_When_ANoticeIsKept()
    {
        SyncNotices notices = new();
        int told = 0;
        notices.Changed += () => told++;

        notices.Keep("colm", 4, SignIn);
        notices.Keep("colm", 4, SignIn with { SignInProblem = "Sign-in needs a connection. Try again when you have signal." });

        Assert.Equal(2, told);
        Assert.Equal("Sign-in needs a connection. Try again when you have signal.", notices.Read("colm", 4)!.SignInProblem);
    }

    // WI-041: a sign-in from the Sync screen asks the new session's Sync screen to carry on with the Sync, once.
    [Fact]
    public void Should_ResumeOnce_When_TheSameRepAsksInTheSessionTheSignInStarted()
    {
        SyncNotices notices = new();
        int told = 0;
        notices.Changed += () => told++;

        notices.RequestResume("colm", 5);

        Assert.Equal(1, told);
        Assert.True(notices.TakeResume("colm", 5));
        Assert.False(notices.TakeResume("colm", 5));
    }

    [Theory]
    [InlineData("aoife", 5)]
    [InlineData("colm", 4)]
    [InlineData("colm", 6)]
    public void Should_NotResume_When_AnotherRepOrSessionAsks(string rep, long version)
    {
        SyncNotices notices = new();
        notices.RequestResume("colm", 5);

        Assert.False(notices.TakeResume(rep, version));
    }
}
