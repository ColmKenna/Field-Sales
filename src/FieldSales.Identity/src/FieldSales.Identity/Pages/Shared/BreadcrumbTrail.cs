namespace FieldSales.Identity.Pages.Shared;

public static class BreadcrumbTrail
{
    /// <summary>
    ///     Resolves a trail for rendering. Every crumb but the first is preceded by a
    ///     separator, and the last crumb is the page being viewed, so it stays plain text
    ///     even when it names a page.
    /// </summary>
    public static IReadOnlyList<BreadcrumbTrailItem> Resolve(IEnumerable<Breadcrumb> crumbs)
    {
        List<Breadcrumb> trail = crumbs.ToList();

        return trail
            .Select((crumb, index) => new BreadcrumbTrailItem(
                crumb.Text,
                crumb.Page,
                crumb.RouteValues,
                IsLink: crumb.Page is not null && index < trail.Count - 1,
                NeedsSeparator: index > 0))
            .ToList();
    }
}
