namespace FieldSales.Identity.Pages.Shared;

/// <summary>
///     Which sidebar section the current request belongs to. Matched on the Razor Pages
///     route rather than the title, because titles vary per sub-page, and on segment
///     boundaries, so "/Admin/Apis" does not also light up on "/Admin/ApiScopes".
/// </summary>
public sealed class AdminNavigationState(string? currentPage)
{
    private readonly string _currentPage = currentPage ?? string.Empty;

    public bool IsActiveSection(string sectionPrefix) =>
        _currentPage.Length > 0 &&
        (_currentPage.Equals(sectionPrefix, StringComparison.OrdinalIgnoreCase) ||
         _currentPage.StartsWith(sectionPrefix + "/", StringComparison.OrdinalIgnoreCase));

    public string ItemClass(string sectionPrefix) =>
        IsActiveSection(sectionPrefix) ? "nav-item active" : "nav-item";
}
