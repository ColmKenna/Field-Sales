namespace FieldSales.Directory.Contracts;

public enum TerritoryLevel { Region, County, Town, Location }

public static class CoverageFields
{
    // Matches the identity store's subject-key limit; subjects remain opaque and case-sensitive.
    public const int MaximumSubjectLength = 450;
}

public sealed record TerritoryTarget(TerritoryLevel Level, Guid UnitId);
public sealed record TerritoryAssignmentDetails(Guid Id, string RepSubject, TerritoryTarget Target, string Version);
public sealed record RepReportingLineDetails(string RepSubject, string ManagerSubject, string Version);

public sealed record AssignedTerritoryDetails(TerritoryAssignmentDetails Assignment, string Name);
public sealed record CoverageSourceDetails(Guid AssignmentId, TerritoryTarget Target, string Name);
public sealed record EffectiveOwnerDetails(string RepSubject, CoverageSourceDetails Source);
// Null Owner explicitly means Unassigned, rather than a missing Location.
public sealed record LocationCoverageDetails(Guid LocationId, string Name, EffectiveOwnerDetails? Owner);

// Current actor and team authority always come from server-side staff data.
public sealed record AddTerritoryAssignmentRequest(string? RepSubject, TerritoryTarget? Target);
public sealed record RemoveTerritoryAssignmentRequest(string? Version);
public sealed record SetRepReportingLineRequest(string? ManagerSubject, string? Version);
public sealed record CoverageError(string Error, string? Field = null);
