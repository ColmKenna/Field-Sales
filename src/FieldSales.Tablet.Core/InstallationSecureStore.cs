namespace FieldSales.Tablet.Core;

/// <summary>Requires an app-local installation marker before using credentials that can survive an iOS uninstall.</summary>
/// <remarks>Initialization is shared by every operation. A failed reset never exposes old credentials or deletes work.</remarks>
public sealed class InstallationSecureStore : ISecureStore
{
    private const string InstallationKey = "fieldsales.installation";
    private readonly ISecureStore inner;
    private readonly string markerPath;
    private readonly Lazy<Task> initialize;

    public InstallationSecureStore(ISecureStore inner, string markerPath)
    {
        this.inner = inner;
        this.markerPath = markerPath;
        initialize = new(InitializeAsync);
    }

    public async Task<string?> GetAsync(string key)
    {
        await initialize.Value.ConfigureAwait(false);
        return await inner.GetAsync(key).ConfigureAwait(false);
    }

    public async Task SetAsync(string key, string value)
    {
        await initialize.Value.ConfigureAwait(false);
        await inner.SetAsync(key, value).ConfigureAwait(false);
    }

    public bool Remove(string key)
    {
        initialize.Value.GetAwaiter().GetResult();
        return inner.Remove(key);
    }

    private async Task InitializeAsync()
    {
        string? marker = File.Exists(markerPath) ? File.ReadAllText(markerPath) : null;
        if (Guid.TryParseExact(marker, "N", out _) && marker == await inner.GetAsync(InstallationKey).ConfigureAwait(false))
            return;

        string? rep = await inner.GetAsync(TabletDevice.CurrentRepKey).ConfigureAwait(false);
        List<string> keys = [TabletDevice.CurrentRepKey, TabletDevice.PreviouslySignedInKey];
        if (rep is not null)
        {
            keys.Add(TabletDevice.SessionKey(rep));
            keys.Add(TokenStore.Key(rep));
        }
        foreach (string key in keys)
        {
            inner.Remove(key);
            if (await inner.GetAsync(key).ConfigureAwait(false) is not null)
                throw new IOException("Previous installation credentials could not be removed.");
        }

        string installation = Guid.NewGuid().ToString("N");
        await inner.SetAsync(InstallationKey, installation).ConfigureAwait(false);
        // Commit the marker last. If writing it fails, another launch repeats the credential reset.
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(markerPath))!);
        string temporary = markerPath + ".tmp";
        File.WriteAllText(temporary, installation);
        File.Move(temporary, markerPath, overwrite: true);
    }
}
