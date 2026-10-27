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
        builder.Services.AddSingleton<ISecureStore, MauiSecureStore>();
#if ANDROID
        builder.Services.AddSingleton<IStorageSpace, AndroidStorageSpace>();
#else
        builder.Services.AddSingleton<IStorageSpace, DriveStorageSpace>();
#endif
        builder.Services.AddSingleton(services => new TabletDevice(FileSystem.AppDataDirectory,
            services.GetRequiredService<ISecureStore>(), services.GetRequiredService<TimeProvider>()));
        builder.Services.AddSingleton(TabletEndpoints.FromBuild());
        builder.Services.AddSingleton<TabletApp>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif
        MauiApp app = builder.Build();
        TabletTiming.Mark("built");
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
        HttpClient Client(string key) => new(new HttpClientHandler { AutomaticDecompression = System.Net.DecompressionMethods.None })
        { BaseAddress = new Uri(Setting(key)), Timeout = TimeSpan.FromMinutes(10) };
        return new(Client("FieldSalesApiBaseUrl"), Client("FieldSalesIdentityBaseUrl"));
    }
}

public sealed class MauiSecureStore : ISecureStore
{
    public Task<string?> GetAsync(string key) => SecureStorage.Default.GetAsync(key);
    public Task SetAsync(string key, string value) => SecureStorage.Default.SetAsync(key, value);
    public bool Remove(string key) => SecureStorage.Default.Remove(key);
}
