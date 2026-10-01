namespace FieldSales.Identity.Pages.Shared;

/// <summary>
///     One breadcrumb with its position in the trail already decided, so the layout only
///     has to choose markup.
/// </summary>
public sealed record BreadcrumbTrailItem(
    string Text,
    string? Page,
    IDictionary<string, string>? RouteValues,
    bool IsLink,
    bool NeedsSeparator);
