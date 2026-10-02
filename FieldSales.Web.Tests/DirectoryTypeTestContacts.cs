using FieldSales.ReferenceData;

namespace FieldSales.Web.Tests;

// WI-017's approved pre-Contact fixture. Production Contact storage and forms are WI-018.
public sealed class DirectoryTypeTestContacts : IReferenceUsageSource
{
    public sealed record ContactRecord(Guid Id, Guid TypeId, bool Inactive);
    private readonly List<ContactRecord> _records = [];
    public IReadOnlyList<ContactRecord> Records => _records.AsReadOnly();
    public string Mode { get; set; } = "valid";
    public string SourceKey => "contacts";
    public bool Supports(string listKey) => listKey == ReferenceListKeys.ContactTypes;
    public void Add(Guid typeId, int count)
    { for (int i = 0; i < count; i++) _records.Add(new(Guid.NewGuid(), typeId, i % 2 == 0)); }
    public void Reset() { _records.Clear(); Mode = "valid"; }
    public Task<ReferenceCount> CountAsync(ReferenceItemKey item, CancellationToken ct) => Task.FromResult(Count(item.ItemId));
    private ReferenceCount Count(Guid id)
    {
        if (Mode == "unavailable") throw new HttpRequestException("Contact usage unavailable.");
        return new(Mode == "wrong-key" ? "unexpected" : SourceKey, "contact", "contacts",
            Mode == "negative" ? -1 : _records.Where(record => record.TypeId == id).Select(record => record.Id).Distinct().LongCount());
    }
    public Task<IReadOnlyDictionary<Guid, ReferenceCount>> CountManyAsync(string key, IReadOnlyList<Guid> ids, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<Guid, ReferenceCount>>(Mode == "incomplete" ? new Dictionary<Guid, ReferenceCount>()
            : ids.Distinct().ToDictionary(id => id, Count));
}
