using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldSales.Web.Pages.Staff;

public sealed class IndexModel(
    IHttpClientFactory clients,
    IConfiguration configuration,
    ILogger<IndexModel> logger) : PageModel
{
    public bool ApiAvailable { get; private set; }

    public async Task OnGetAsync()
    {
        string? token = await HttpContext.GetTokenAsync("access_token");
        if (string.IsNullOrWhiteSpace(token)) return;

        string baseUrl = configuration["StaffApi:BaseUrl"]
            ?? throw new InvalidOperationException("StaffApi:BaseUrl is required.");
        using HttpClient client = clients.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, $"{baseUrl.TrimEnd('/')}/staff/session");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        try
        {
            using HttpResponseMessage response = await client.SendAsync(request, HttpContext.RequestAborted);
            ApiAvailable = response.IsSuccessStatusCode;
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "The staff API session could not be checked.");
        }
    }
}
