using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using FieldSales.Web.Security;

namespace FieldSales.Web.Pages.Staff;

public sealed class IndexModel(
    IHttpClientFactory clients,
    IConfiguration configuration,
    ILogger<IndexModel> logger,
    StaffAreaService areaService) : PageModel
{
    public bool ApiAvailable { get; private set; }
    public IReadOnlyList<StaffArea> PermittedAreas { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        PermittedAreas = StaffAreas.PermittedTo(User);
        string? lastArea = await areaService.GetLastPermittedAreaAsync(User, HttpContext.RequestAborted);
        if (lastArea is not null) return LocalRedirect(lastArea);
        if (PermittedAreas.Count == 1) return LocalRedirect(PermittedAreas[0].Route);
        if (PermittedAreas.Count == 0) return RedirectToPage("/AccessDenied");

        string? token = await HttpContext.GetTokenAsync("access_token");
        if (string.IsNullOrWhiteSpace(token)) return Page();

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
        return Page();
    }
}
