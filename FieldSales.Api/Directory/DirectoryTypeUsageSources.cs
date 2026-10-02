using FieldSales.ReferenceData;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Directory;

public sealed class LocationTypeUsageSource(DirectoryDbContext db) : IReferenceUsageSource
{
    public string SourceKey => "locations";
    public bool Supports(string listKey) => listKey == ReferenceListKeys.LocationTypes;
    public async Task<ReferenceCount> CountAsync(ReferenceItemKey item, CancellationToken ct) =>
        new(SourceKey, "location", "locations", await db.Locations.LongCountAsync(location => location.LocationTypeId == item.ItemId, ct));
    public async Task<IReadOnlyDictionary<Guid, ReferenceCount>> CountManyAsync(string listKey, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        var counts = await db.Locations.Where(location => location.LocationTypeId != null && ids.Contains(location.LocationTypeId.Value))
            .GroupBy(location => location.LocationTypeId!.Value).Select(group => new { Id = group.Key, Count = group.LongCount() })
            .ToDictionaryAsync(row => row.Id, row => row.Count, ct);
        return ids.Distinct().ToDictionary(id => id, id => new ReferenceCount(SourceKey, "location", "locations", counts.GetValueOrDefault(id)));
    }
}

/// <summary>WI-018 must replace this keyed provider with persisted Contact counts,
/// including Inactive Contacts, using the same scoped DirectoryDb transaction.</summary>
public sealed class EmptyContactTypeUsageSource : IReferenceUsageSource
{
    public string SourceKey => "contacts";
    public bool Supports(string listKey) => listKey == ReferenceListKeys.ContactTypes;
    public Task<ReferenceCount> CountAsync(ReferenceItemKey item, CancellationToken ct) => Task.FromResult(new ReferenceCount(SourceKey, "contact", "contacts", 0));
    public Task<IReadOnlyDictionary<Guid, ReferenceCount>> CountManyAsync(string listKey, IReadOnlyList<Guid> ids, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<Guid, ReferenceCount>>(ids.Distinct().ToDictionary(id => id, _ => new ReferenceCount(SourceKey, "contact", "contacts", 0)));
}
