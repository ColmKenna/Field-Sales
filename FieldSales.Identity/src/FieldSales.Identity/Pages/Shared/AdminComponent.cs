namespace FieldSales.Identity.Pages.Shared;

/// <summary>
///     Web component modules a page needs. The shared admin layout loads only what the page
///     declares, so a page using neither component requests neither module.
/// </summary>
[Flags]
public enum AdminComponent
{
    None = 0,
    ResponsiveTable = 1,
    Tabs = 2
}
