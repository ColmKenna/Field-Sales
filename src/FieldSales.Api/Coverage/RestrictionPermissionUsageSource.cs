using FieldSales.Api.Catalogue;
using FieldSales.ReferenceData;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Coverage;

// Counts the reps who currently hold each Restriction Group's permission. It reads the catalogue context the
// product source uses, so a retirement's count shares its delete transaction (removed permissions don't count).
public sealed class RestrictionPermissionUsageSource(CatalogueDbContext db) : IReferenceUsageSource
{
    public string SourceKey => RestrictionGroupListStore.PermissionSource;
    public bool Supports(string listKey) => listKey == ReferenceListKeys.RestrictionGroups;

    public async Task<ReferenceCount> CountAsync(ReferenceItemKey item, CancellationToken cancellationToken) =>
        Count(await db.RestrictionPermissions.LongCountAsync(row => row.RestrictionGroupId == item.ItemId, cancellationToken));

    public async Task<IReadOnlyDictionary<Guid, ReferenceCount>> CountManyAsync(string listKey,
        IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        var counts = await db.RestrictionPermissions.Where(row => ids.Contains(row.RestrictionGroupId))
            .GroupBy(row => row.RestrictionGroupId).Select(group => new { Id = group.Key, Count = group.LongCount() })
            .ToDictionaryAsync(group => group.Id, group => group.Count, cancellationToken);
        return ids.Distinct().ToDictionary(id => id, id => Count(counts.GetValueOrDefault(id)));
    }

    private ReferenceCount Count(long count) => new(SourceKey, "permission", "permissions", count);
}
