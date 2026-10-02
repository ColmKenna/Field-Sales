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

/// <summary>Counts people, including Inactive Contacts, not their Location links.
/// Retirement uses the same scoped DirectoryDb transaction as these reads.</summary>
public sealed class ContactTypeUsageSource(DirectoryDbContext db) : IReferenceUsageSource
{
    public string SourceKey => "contacts";
    public bool Supports(string listKey) => listKey == ReferenceListKeys.ContactTypes;
    public async Task<ReferenceCount> CountAsync(ReferenceItemKey item, CancellationToken ct) =>
        new(SourceKey, "contact", "contacts", await db.Contacts.LongCountAsync(contact => contact.ContactTypeId == item.ItemId, ct));
    public async Task<IReadOnlyDictionary<Guid, ReferenceCount>> CountManyAsync(string listKey, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        var counts = await db.Contacts.Where(contact => ids.Contains(contact.ContactTypeId)).GroupBy(contact => contact.ContactTypeId)
            .Select(group => new { Id = group.Key, Count = group.LongCount() }).ToDictionaryAsync(row => row.Id, row => row.Count, ct);
        return ids.Distinct().ToDictionary(id => id, id => new ReferenceCount(SourceKey, "contact", "contacts", counts.GetValueOrDefault(id)));
    }
}
