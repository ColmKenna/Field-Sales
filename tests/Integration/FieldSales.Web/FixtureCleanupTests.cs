namespace FieldSales.Web.Tests;

public sealed class FixtureCleanupTests
{
    [Fact]
    public async Task PartialSetupIsUnwoundAndOriginalFailureIsPreserved()
    {
        var failure = new InvalidOperationException("setup failed");
        bool cleaned = false;
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => FixtureCleanup.OnFailureAsync(
            () => Task.FromException(failure), () => { cleaned = true; return Task.CompletedTask; }));
        Assert.Same(failure, actual);
        Assert.True(cleaned);
    }

    [Fact]
    public async Task CleanupFailureIsAttachedWithoutReplacingSetupFailure()
    {
        var failure = new InvalidOperationException("setup failed");
        var cleanup = new IOException("cleanup failed");
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => FixtureCleanup.OnFailureAsync(
            () => Task.FromException(failure), () => Task.FromException(cleanup)));
        Assert.Same(failure, actual);
        Assert.Same(cleanup, actual.Data["FixtureCleanupFailure"]);
    }

    [Fact]
    public async Task SuccessfulSetupRetainsOwnedResources()
    {
        bool cleaned = false;
        await FixtureCleanup.OnFailureAsync(() => Task.CompletedTask,
            () => { cleaned = true; return Task.CompletedTask; });
        Assert.False(cleaned);
    }

    [Fact]
    public async Task ApplicationsCanBeDisposedRepeatedlyBeforeInitialization()
    {
        var product = new ProductApplication(new CatalogueSqlServerFixture());
        var geography = new GeographyApplication(new DirectorySqlServerFixture());
        await product.DisposeAsync();
        await product.DisposeAsync();
        await geography.DisposeAsync();
        await geography.DisposeAsync();
    }
}
