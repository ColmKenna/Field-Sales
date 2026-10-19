using FieldSales.Api.Catalogue;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Coverage;

// A rep's current Restriction Permissions for the tablet snapshot builder (WI-043). Pass them to
// RestrictedProductVisibility.VisibleToRep, which also hides an archived group's products; don't re-derive
// visibility. Archived groups the rep holds are included, so un-archiving restores them. The read joins the
// caller's catalogue transaction, if any, so permissions and products can be read from one snapshot.
public sealed class RestrictionPermissionReader(CatalogueDbContext db)
{
    public async Task<IReadOnlyCollection<Guid>> CurrentGroupIdsAsync(string repSubject, CancellationToken ct)
    {
        repSubject = CoverageSubjects.Validate(repSubject, "RepSubject");
        return await db.RestrictionPermissions.AsNoTracking().Where(row => row.RepSubject == repSubject)
            .Select(row => row.RestrictionGroupId).ToArrayAsync(ct);
    }
}
