namespace FieldSales.Identity.Pages.Shared;

/// <summary>
///     One crumb in the header trail. Carries a Razor Page name rather than a URL so the
///     layout can generate the href through routing.
/// </summary>
/// <remarks>
///     Hand-written paths hid a real defect: Users/Details is declared <c>@page</c> with no
///     route template, so it lives at <c>?id=…</c>, but a breadcrumb wrote it as
///     <c>/Admin/Users/Details/{id}</c> — a path that matched no route. Routing knows each
///     page's real shape; a literal only knows what someone typed.
///     <para>A crumb with no <see cref="Page" /> renders as the current, unlinked item.</para>
/// </remarks>
public sealed record Breadcrumb(
    string Text,
    string? Page = null,
    IDictionary<string, string>? RouteValues = null);
