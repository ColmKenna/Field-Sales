using FieldSales.Tablet.Core;

namespace FieldSales.Tablet.Core.UnitTests;

public sealed class InstallationSecureStoreTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "fieldsales-install-" + Guid.NewGuid().ToString("N"));
    private string Marker => Path.Combine(root, "installation");

    [Fact]
    public async Task ReinstallClearsCredentialsButPreservesWorkAndUnrelatedKeys()
    {
        MemorySecureStore inner = new();
        InstallationSecureStore first = new(inner, Marker);
        await first.SetAsync("fieldsales.current-rep", "rep");
        await first.SetAsync("fieldsales.session.rep", "session");
        await first.SetAsync("fieldsales.tokens.rep", "tokens");
        await first.SetAsync("fieldsales.previously-signed-in", "true");
        await inner.SetAsync("unrelated", "keep");
        string work = Path.Combine(root, "unsent.db");
        File.WriteAllText(work, "saved work");
        File.Delete(Marker);

        InstallationSecureStore reinstall = new(inner, Marker);
        foreach (string key in new[] { "fieldsales.current-rep", "fieldsales.session.rep", "fieldsales.tokens.rep", "fieldsales.previously-signed-in" })
            Assert.Null(await reinstall.GetAsync(key));
        Assert.Equal("keep", await inner.GetAsync("unrelated"));
        Assert.Equal("saved work", File.ReadAllText(work));
    }

    [Fact]
    public async Task RelaunchWithMatchingMarkerKeepsSession()
    {
        MemorySecureStore inner = new();
        await new InstallationSecureStore(inner, Marker).SetAsync("fieldsales.current-rep", "rep");
        InstallationSecureStore relaunch = new(inner, Marker);
        var reads = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => relaunch.GetAsync("fieldsales.current-rep")));
        Assert.All(reads, value => Assert.Equal("rep", value));
    }

    [Theory]
    [InlineData("corrupt")]
    [InlineData("00000000000000000000000000000000")]
    public async Task InvalidOrMismatchedMarkerClearsSession(string marker)
    {
        MemorySecureStore inner = new();
        await new InstallationSecureStore(inner, Marker).SetAsync("fieldsales.current-rep", "rep");
        File.WriteAllText(Marker, marker);
        Assert.Null(await new InstallationSecureStore(inner, Marker).GetAsync("fieldsales.current-rep"));
    }

    [Fact]
    public async Task FailedCredentialRemovalFailsClosedAndDoesNotCommitMarker()
    {
        MemorySecureStore inner = new() { RefuseRemoval = true };
        await inner.SetAsync("fieldsales.current-rep", "rep");
        InstallationSecureStore store = new(inner, Marker);
        await Assert.ThrowsAsync<IOException>(() => store.GetAsync("fieldsales.current-rep"));
        await Assert.ThrowsAsync<IOException>(() => store.SetAsync("new-key", "value"));
        Assert.False(File.Exists(Marker));
        Assert.Null(await inner.GetAsync("new-key"));
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }

    private sealed class MemorySecureStore : ISecureStore
    {
        private readonly Dictionary<string, string> values = [];
        public bool RefuseRemoval { get; init; }
        public Task<string?> GetAsync(string key) => Task.FromResult(values.GetValueOrDefault(key));
        public Task SetAsync(string key, string value) { values[key] = value; return Task.CompletedTask; }
        public bool Remove(string key) => !RefuseRemoval && values.Remove(key);
    }
}
