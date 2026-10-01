namespace FieldSales.Api.Catalogue;

/// <summary>The single rule deciding which products a rep may see. The snapshot builder (WI-043) applies it.</summary>
/// <remarks>
/// A product with no Restriction Group is visible to every rep. A product in an active group is visible only
/// to reps holding that group's permission. While a group is archived its permissions grant nothing, so its
/// products are hidden from every rep (MI-45, decided 2026-10-01). A missing group also hides the product.
/// </remarks>
public static class RestrictedProductVisibility
{
    /// <param name="products">The products to filter; compose further query operators before or after.</param>
    /// <param name="groups">The catalogue's Restriction Groups, for example <c>db.RestrictionGroups</c>.</param>
    /// <param name="permittedGroupIds">The rep's current Restriction Permissions (WI-029); empty for a new rep.</param>
    public static IQueryable<Product> VisibleToRep(this IQueryable<Product> products,
        IQueryable<RestrictionGroup> groups, IReadOnlyCollection<Guid> permittedGroupIds)
    {
        ArgumentNullException.ThrowIfNull(permittedGroupIds);
        return products.Where(product => product.RestrictionGroupId == null
            || groups.Any(group => group.Id == product.RestrictionGroupId
                && !group.IsArchived && permittedGroupIds.Contains(group.Id)));
    }
}
