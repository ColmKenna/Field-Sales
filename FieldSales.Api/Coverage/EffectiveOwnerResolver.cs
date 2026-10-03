using FieldSales.Directory.Contracts;

namespace FieldSales.Api.Coverage;

// The caller supplies the current, authoritative hierarchy. This rule never
// queries a store, checks staff roles, or writes owner/history state.
public sealed record LocationOwnershipPath(Guid LocationId, string LocationName,
    Guid TownId, string TownName, Guid CountyId, string CountyName, Guid RegionId, string RegionName);

public sealed record EffectiveOwnerSource(Guid AssignmentId, TerritoryTarget Target, string Name);
public sealed record EffectiveOwner(string RepSubject, EffectiveOwnerSource Source);

/// <summary>One most-specific rule for reads, previews and future snapshots.</summary>
public sealed class EffectiveOwnerResolver
{
    private readonly Dictionary<(TerritoryLevel Level, Guid UnitId), AssignedRep> assignments = [];

    // Copy assignment values once, so later changes to the input collection
    // cannot change this snapshot. Resolve then needs at most four lookups.
    public EffectiveOwnerResolver(IEnumerable<TerritoryAssignment> snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        foreach (var assignment in snapshot)
        {
            var target = assignment.Target;
            if (!assignments.TryAdd((target.Level, target.UnitId), new(assignment.Id, assignment.RepSubject)))
                throw new InvalidOperationException("More than one assignment targets the same territory or location.");
        }
    }

    /// <returns>The rep and winning source, or null when the Location is Unassigned.</returns>
    public EffectiveOwner? Resolve(LocationOwnershipPath location)
    {
        ArgumentNullException.ThrowIfNull(location);
        return At(TerritoryLevel.Location, location.LocationId, location.LocationName)
            ?? At(TerritoryLevel.Town, location.TownId, location.TownName)
            ?? At(TerritoryLevel.County, location.CountyId, location.CountyName)
            ?? At(TerritoryLevel.Region, location.RegionId, location.RegionName);
    }

    private EffectiveOwner? At(TerritoryLevel level, Guid unitId, string name) =>
        assignments.TryGetValue((level, unitId), out var assigned)
            ? new(assigned.RepSubject, new(assigned.AssignmentId, new(level, unitId), name)) : null;

    private sealed record AssignedRep(Guid AssignmentId, string RepSubject);
}
