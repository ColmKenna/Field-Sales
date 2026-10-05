using System.ComponentModel.DataAnnotations;
using System.Net;
using FieldSales.Directory.Contracts;
using FieldSales.Web.Coverage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldSales.Web.Pages.HeadOffice.Coverage;

public sealed class ReportingLinesModel(CoverageApiClient coverage) : PageModel
{
    [BindProperty(SupportsGet = true), Required(ErrorMessage = "Choose a field salesperson.")]
    public string? RepSubject { get; set; }
    [BindProperty, Required(ErrorMessage = "Choose a Sales Manager.")] public string? ManagerSubject { get; set; }
    [BindProperty] public string? Version { get; set; }
    public ReportingLinesPage Data { get; private set; } = new([], [], []);
    public string? RepName => Data.Reps.SingleOrDefault(rep => rep.Subject == RepSubject)?.Name;

    public async Task<IActionResult> OnGetAsync()
    {
        var failure = await LoadAsync(); if (failure is not null) return failure;
        var current = Data.Lines.SingleOrDefault(row => row.Line.RepSubject == RepSubject);
        ManagerSubject = current?.Line.ManagerSubject; Version = current?.Line.Version;
        ModelState.Clear();
        return Page();
    }
    public async Task<IActionResult> OnPostAsync()
    {
        if (ModelState.IsValid)
        {
            var saved = await coverage.SetReportingLineAsync(RepSubject!, new(ManagerSubject, string.IsNullOrEmpty(Version) ? null : Version), HttpContext.RequestAborted);
            if (saved.Success)
            {
                TempData["CoverageNotice"] = "Reporting line saved.";
                return RedirectToPage(new { repSubject = RepSubject });
            }
            if (saved.Status is not (HttpStatusCode.BadRequest or HttpStatusCode.Conflict)) return StatusCode((int)saved.Status);
            ModelState.AddModelError(saved.Field ?? string.Empty, saved.Error ?? "This change could not be saved.");
        }
        return await LoadAsync() ?? Page();
    }
    private async Task<IActionResult?> LoadAsync()
    {
        var result = await coverage.ReportingLinesAsync(HttpContext.RequestAborted);
        if (!result.Success) return StatusCode((int)result.Status);
        Data = result.Value!; return null;
    }
}
