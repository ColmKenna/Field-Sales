using FieldSales.ReferenceData;

namespace FieldSales.Web.Tests;

/// <summary>Referencing records for the approved WI-015 fixture; WI-016 owns production Location storage.</summary>
public sealed class TestLocationUsageSource : IReferenceUsageSource
{
    public sealed record Location(Guid Id, Guid TownId);
    public List<Location> Records { get; } = [];
    public string? Failure { get; set; }
    public string SourceKey => Failure == "missing" ? "unexpected" : "locations";
    public bool Supports(string listKey) => listKey == "towns";
    public void Reset() { Records.Clear(); Failure = null; }
    public void Add(Guid townId, int count)
    {
        for (int index = 0; index < count; index++) Records.Add(new(Guid.NewGuid(), townId));
    }
    public Task<ReferenceCount> CountAsync(ReferenceItemKey item, CancellationToken ct)
    {
        if (Failure == "unavailable") throw new InvalidOperationException("Fixture unavailable.");
        return Task.FromResult(new ReferenceCount(Failure == "wrong" ? "other" : "locations", "location", "locations",
            Failure == "negative" ? -1 : Records.Where(location => location.TownId == item.ItemId).Select(location => location.Id).Distinct().LongCount()));
    }
    public async Task<IReadOnlyDictionary<Guid, ReferenceCount>> CountManyAsync(string listKey, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        var result = new Dictionary<Guid, ReferenceCount>();
        foreach (Guid id in ids.Distinct()) result[id] = await CountAsync(new(listKey, id), ct);
        if (Failure == "incomplete") result.Clear();
        return result;
    }
}
