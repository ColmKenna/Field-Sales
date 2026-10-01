using System.Text.Json.Serialization;

namespace FieldSales.ReferenceData;

public readonly record struct ReferenceItemKey(string ListKey, Guid ItemId);
public sealed record ReferenceCount(string SourceKey, string SingularLabel, string PluralLabel, long Count);

/// <summary>Reports distinct referencing records. Retirement reads must share the delete transaction.</summary>
public interface IReferenceUsageSource
{
    string SourceKey { get; }
    bool Supports(string listKey);
    Task<ReferenceCount> CountAsync(ReferenceItemKey item, CancellationToken cancellationToken);

    // Small/custom providers can use this sequential fallback; SQL catalogue providers override it.
    async Task<IReadOnlyDictionary<Guid, ReferenceCount>> CountManyAsync(string listKey,
        IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        Dictionary<Guid, ReferenceCount> counts = [];
        foreach (Guid id in ids.Distinct()) counts[id] = await CountAsync(new(listKey, id), cancellationToken);
        return counts;
    }
}

public interface IReferenceUsageReader
{
    Task<ReferenceUsage> ReadAsync(ReferenceItemKey item, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<Guid, ReferenceUsage>> ReadManyAsync(string listKey,
        IReadOnlyList<Guid> ids, CancellationToken cancellationToken);
}

public sealed class ReferenceUsage
{
    [JsonConstructor]
    public ReferenceUsage(IReadOnlyList<ReferenceCount> counts)
    {
        ArgumentNullException.ThrowIfNull(counts);
        if (counts.Any(count => count is null || count.Count < 0
                || string.IsNullOrWhiteSpace(count.SourceKey)
                || string.IsNullOrWhiteSpace(count.SingularLabel)
                || string.IsNullOrWhiteSpace(count.PluralLabel))
            || counts.Select(count => count.SourceKey).Distinct(StringComparer.Ordinal).Count() != counts.Count)
            throw new ArgumentException("Reference counts must have unique source keys, labels and nonnegative counts.", nameof(counts));
        Counts = Array.AsReadOnly(counts.ToArray());
    }

    public IReadOnlyList<ReferenceCount> Counts { get; }
    public bool IsUsed => Counts.Any(count => count.Count > 0);
    public string Description
    {
        get
        {
            string[] parts = Counts.Where(count => count.Count > 0)
                .Select(count => $"{count.Count} {(count.Count == 1 ? count.SingularLabel : count.PluralLabel)}").ToArray();
            return parts.Length == 0 ? "Not used yet" : "Used by " + (parts.Length == 1 ? parts[0]
                : string.Join(", ", parts[..^1]) + " and " + parts[^1]);
        }
    }
}

public enum ReferenceAction { Delete, Archive, Unarchive }
public static class ReferenceActionExtensions
{
    public static string ToLabel(this ReferenceAction action) => action switch
    {
        ReferenceAction.Delete => "Delete",
        ReferenceAction.Archive => "Archive",
        ReferenceAction.Unarchive => "Un-archive",
        _ => throw new ArgumentOutOfRangeException(nameof(action))
    };
}
public static class ReferenceRetirementPolicy
{
    public static ReferenceAction Decide(bool isArchived, ReferenceUsage usage) =>
        isArchived ? ReferenceAction.Unarchive : usage.IsUsed ? ReferenceAction.Archive : ReferenceAction.Delete;
}

public sealed record ReferenceListDefinition(string Key, string SingularLabel, string PluralLabel,
    IReadOnlyList<string> RequiredUsageSources);
public sealed record ReferenceListItem(Guid Id, string Name, bool IsArchived, ReferenceUsage Usage);
public sealed record ReferenceListViewModel(ReferenceListDefinition SelectedList,
    IReadOnlyList<ReferenceListDefinition> AvailableLists, IReadOnlyList<ReferenceListItem> Items,
    int ArchivedCount, bool ShowArchived);
