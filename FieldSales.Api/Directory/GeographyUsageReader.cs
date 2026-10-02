using FieldSales.Api.Catalogue;
using FieldSales.ReferenceData;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Directory;

public static class GeographyLists
{
    public static ReferenceListDefinition Definition(string level) => level switch
    {
        "regions" => new(level, "Region", "Regions", ["counties"]),
        "counties" => new(level, "County", "Counties", ["towns"]),
        "towns" => new(level, "Town", "Towns", ["locations"]),
        _ => throw new GeographyValidationException("Choose a geography level.")
    };
}

/// <summary>Directory sources use the request's DirectoryDbContext and retirement transaction.</summary>
public sealed class GeographyUsageReader([FromKeyedServices("directory")] IEnumerable<IReferenceUsageSource> sources) : IReferenceUsageReader
{
    public async Task<ReferenceUsage> ReadAsync(ReferenceItemKey item, CancellationToken ct) =>
        (await ReadManyAsync(item.ListKey, [item.ItemId], ct))[item.ItemId];

    public async Task<IReadOnlyDictionary<Guid, ReferenceUsage>> ReadManyAsync(string level,
        IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        try
        {
            var required = GeographyLists.Definition(level).RequiredUsageSources;
            var applicable = sources.Where(source => source.Supports(level)).ToArray();
            if (applicable.Select(source => source.SourceKey).Distinct().Count() != applicable.Length
                || required.Any(key => !applicable.Any(source => source.SourceKey == key)))
                throw new InvalidOperationException("Required geography usage sources are missing or duplicated.");
            var counts = ids.Distinct().ToDictionary(id => id, _ => new List<ReferenceCount>());
            foreach (var source in applicable)
            {
                if (counts.Count == 0) continue;
                var batch = await source.CountManyAsync(level, counts.Keys.ToArray(), ct);
                if (batch.Count != counts.Count) throw new InvalidOperationException("Incomplete geography usage.");
                foreach (var pair in counts)
                {
                    if (!batch.TryGetValue(pair.Key, out var count) || count.SourceKey != source.SourceKey)
                        throw new InvalidOperationException("Unexpected geography usage source.");
                    pair.Value.Add(count);
                }
            }
            return counts.ToDictionary(pair => pair.Key, pair => new ReferenceUsage(pair.Value));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        { throw new ReferenceUsageUnavailableException("Geography usage could not be checked.", exception); }
    }
}

public sealed class CountyRegionUsageSource(DirectoryDbContext db) : IReferenceUsageSource
{
    public string SourceKey => "counties";
    public bool Supports(string listKey) => listKey == "regions";
    public async Task<ReferenceCount> CountAsync(ReferenceItemKey item, CancellationToken ct) =>
        new(SourceKey, "county", "counties", await db.Counties.LongCountAsync(county => county.RegionId == item.ItemId, ct));
    public async Task<IReadOnlyDictionary<Guid, ReferenceCount>> CountManyAsync(string listKey, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        var counts = await db.Counties.Where(county => ids.Contains(county.RegionId)).GroupBy(county => county.RegionId)
            .Select(group => new { Id = group.Key, Count = group.LongCount() }).ToDictionaryAsync(row => row.Id, row => row.Count, ct);
        return ids.Distinct().ToDictionary(id => id, id => new ReferenceCount(SourceKey, "county", "counties", counts.GetValueOrDefault(id)));
    }
}

public sealed class TownCountyUsageSource(DirectoryDbContext db) : IReferenceUsageSource
{
    public string SourceKey => "towns";
    public bool Supports(string listKey) => listKey == "counties";
    public async Task<ReferenceCount> CountAsync(ReferenceItemKey item, CancellationToken ct) =>
        new(SourceKey, "town", "towns", await db.Towns.LongCountAsync(town => town.CountyId == item.ItemId, ct));
    public async Task<IReadOnlyDictionary<Guid, ReferenceCount>> CountManyAsync(string listKey, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        var counts = await db.Towns.Where(town => ids.Contains(town.CountyId)).GroupBy(town => town.CountyId)
            .Select(group => new { Id = group.Key, Count = group.LongCount() }).ToDictionaryAsync(row => row.Id, row => row.Count, ct);
        return ids.Distinct().ToDictionary(id => id, id => new ReferenceCount(SourceKey, "town", "towns", counts.GetValueOrDefault(id)));
    }
}

public sealed class LocationTownUsageSource(DirectoryDbContext db) : IReferenceUsageSource
{
    public string SourceKey => "locations";
    public bool Supports(string listKey) => listKey == "towns";
    public async Task<ReferenceCount> CountAsync(ReferenceItemKey item, CancellationToken ct) =>
        new(SourceKey, "location", "locations", await db.Locations.LongCountAsync(location => location.TownId == item.ItemId, ct));
    public async Task<IReadOnlyDictionary<Guid, ReferenceCount>> CountManyAsync(string listKey, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        var counts = await db.Locations.Where(location => ids.Contains(location.TownId)).GroupBy(location => location.TownId)
            .Select(group => new { Id = group.Key, Count = group.LongCount() }).ToDictionaryAsync(row => row.Id, row => row.Count, ct);
        return ids.Distinct().ToDictionary(id => id, id => new ReferenceCount(SourceKey, "location", "locations", counts.GetValueOrDefault(id)));
    }
}
