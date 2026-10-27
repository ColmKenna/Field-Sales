using Microsoft.Extensions.Logging;

namespace FieldSales.TabletSpike;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		builder.Services.AddMauiBlazorWebView();

		builder.Services.AddSingleton<DeviceReportAdapter>();
		builder.Services.AddSingleton(new HttpClient(new HttpClientHandler
		{
			AutomaticDecompression = System.Net.DecompressionMethods.None
		}) { Timeout = TimeSpan.FromMinutes(30) });

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
