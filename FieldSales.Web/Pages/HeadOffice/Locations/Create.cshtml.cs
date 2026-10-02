using FieldSales.Directory.Contracts;
using FieldSales.Web.Directory;
using Microsoft.AspNetCore.Mvc;

namespace FieldSales.Web.Pages.HeadOffice.Locations;

public sealed class CreateModel(DirectoryApiClient directory) : LocationFormPageModel(directory)
{
    public CustomerDetails Customer { get; private set; } = null!;
    public Task<IActionResult> OnGetAsync(Guid customerId) => ShowAsync(customerId);
    public Task<IActionResult> OnPostAsync(Guid customerId) => SaveAsync(customerId, false);
    public Task<IActionResult> OnPostConfirmAsync(Guid customerId) => SaveAsync(customerId, true);
    private async Task<IActionResult> SaveAsync(Guid customerId, bool confirmed)
    {
        if (!ModelState.IsValid) return await ShowAsync(customerId);
        var result = await Directory.AddLocationAsync(customerId, new(Name, TownId, Eircode, confirmed, LocationTypeId), HttpContext.RequestAborted);
        if (result.Success) return RedirectToPage("Detail", new { id = result.Value!.Id });
        return AddSaveError(result) ?? await ShowAsync(customerId);
    }
    private async Task<IActionResult> ShowAsync(Guid customerId)
    {
        var result = await Directory.CustomerAsync(customerId, HttpContext.RequestAborted);
        if (!result.Success) return StatusCode((int)result.Status);
        Customer = result.Value!;
        return await FormAsync();
    }
}
