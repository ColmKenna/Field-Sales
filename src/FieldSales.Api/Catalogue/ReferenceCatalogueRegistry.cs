using FieldSales.ReferenceData;

namespace FieldSales.Api.Catalogue;

/// <summary>Scoped resolution of stores and the usage sources required to retire their rows safely.</summary>
public sealed class ReferenceCatalogueRegistry
{
    private readonly Dictionary<string, IReferenceListStore> stores;
    private readonly IReferenceUsageSource[] sources;

    public ReferenceCatalogueRegistry(IEnumerable<IReferenceListStore> stores, IEnumerable<IReferenceUsageSource> sources)
    {
        this.stores = stores.ToDictionary(store => store.Definition.Key, StringComparer.Ordinal);
        this.sources = sources.ToArray();
    }

    public IReadOnlyList<ReferenceListDefinition> Definitions => stores.Values.Select(store => store.Definition).ToArray();
    public IReferenceListStore? Find(string key) => stores.GetValueOrDefault(key);

    public IReadOnlyList<IReferenceUsageSource> SourcesFor(string key)
    {
        ReferenceListDefinition definition = Find(key)?.Definition
            ?? throw new InvalidOperationException("Unknown reference list.");
        IReferenceUsageSource[] applicable = this.sources.Where(source => source.Supports(key)).ToArray();
        if (applicable.Select(source => source.SourceKey).Distinct(StringComparer.Ordinal).Count() != applicable.Length
            || definition.RequiredUsageSources.Any(required => !applicable.Any(source => source.SourceKey == required)))
            throw new InvalidOperationException("Required reference sources are missing or duplicated.");
        return applicable;
    }

    public void ValidateRegistrations()
    {
        foreach (string key in stores.Keys) SourcesFor(key);
    }
}
