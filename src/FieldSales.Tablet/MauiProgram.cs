using System.Reflection;
using FieldSales.Tablet.Core;
using Microsoft.Extensions.Logging;

namespace FieldSales.Tablet;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        TabletTiming.Mark("create");
        MauiAppBuilder builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>().ConfigureFonts(fonts => fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"));
        builder.Services.AddMauiBlazorWebView();

        builder.Services.AddSingleton(TimeProvider.System);
#if ANDROID
        string dataDirectory = FileSystem.AppDataDirectory;
        builder.Services.AddSingleton<ISecureStore, MauiSecureStore>();
        builder.Services.AddSingleton<IStorageSpace, AndroidStorageSpace>();
#elif IOS
        string dataDirectory = IosTabletStorage.Prepare();
        builder.Services.AddSingleton<ISecureStore>(_ => new InstallationSecureStore(
            new MauiSecureStore(), Path.Combine(dataDirectory, "installation")));
        builder.Services.AddSingleton<IStorageSpace, IosStorageSpace>();
#else
        string dataDirectory = FileSystem.AppDataDirectory;
        builder.Services.AddSingleton<ISecureStore, MauiSecureStore>();
        builder.Services.AddSingleton<IStorageSpace, DriveStorageSpace>();
#endif
        builder.Services.AddSingleton(services => new TabletDevice(dataDirectory,
            services.GetRequiredService<ISecureStore>(), services.GetRequiredService<TimeProvider>()));
        builder.Services.AddSingleton(TabletEndpoints.FromBuild());
        builder.Services.AddSingleton<TabletApp>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif
        MauiApp app = builder.Build();
        TabletTiming.Mark("built");
#if DEBUG
        if (Environment.GetEnvironmentVariable("FIELDSALES_NETWORK_PROBE") == "1")
            _ = TabletNetworkDiagnostics.ProbeAsync(app.Services.GetRequiredService<TabletEndpoints>());
#endif
        return app;
    }
}

/// <summary>The API and identity host this build talks to (set by the build; see the project file).</summary>
public sealed record TabletEndpoints(HttpClient Api, HttpClient Identity)
{
    public static TabletEndpoints FromBuild()
    {
        string Setting(string key) => typeof(TabletEndpoints).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == key).Value!;
        // Raw gzip bytes: the snapshot's hashes cover exactly what the server sent.
        HttpClient Client(string key)
        {
            HttpMessageHandler handler = new HttpClientHandler { AutomaticDecompression = System.Net.DecompressionMethods.None };
#if DEBUG
            handler = new TabletNetworkDiagnostics { InnerHandler = handler };
#endif
            return new(handler) { BaseAddress = new Uri(Setting(key)), Timeout = TimeSpan.FromMinutes(10) };
        }
        return new(Client("FieldSalesApiBaseUrl"), Client("FieldSalesIdentityBaseUrl"));
    }
}

#if DEBUG
/// <summary>Diagnose device transport failures without logging credentials, headers, bodies or URL queries.</summary>
internal sealed class TabletNetworkDiagnostics : DelegatingHandler
{
    internal static async Task ProbeAsync(TabletEndpoints endpoints)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(15));
        foreach (var (client, path) in new[] { (endpoints.Identity, ".well-known/openid-configuration"), (endpoints.Api, "staff/session") })
        {
            try { using HttpResponseMessage response = await client.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, timeout.Token); }
            catch (HttpRequestException) { } // The handler logs transport details without response bodies or credentials.
            catch (OperationCanceledException) { break; }
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
            TabletTiming.NetworkRequest(request.RequestUri!, (int)response.StatusCode, null);
            return response;
        }
        catch (HttpRequestException error)
        {
            TabletTiming.NetworkRequest(request.RequestUri!, null, error);
            throw;
        }
    }
}
#endif

public sealed class MauiSecureStore : ISecureStore
{
    public Task<string?> GetAsync(string key) => SecureStorage.Default.GetAsync(key);
    public Task SetAsync(string key, string value) => SecureStorage.Default.SetAsync(key, value);
    public bool Remove(string key) => SecureStorage.Default.Remove(key);
}
