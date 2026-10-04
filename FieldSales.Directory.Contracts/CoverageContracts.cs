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

public enum OwnershipChangeCause { DirectLocationAssignment, TerritoryAssignment, GeographyChange }
public sealed record HistoryIdentityDetails(string Subject, string DisplayName);
public sealed record HistoryOwnerDetails(HistoryIdentityDetails Rep, CoverageSourceDetails Source);
public sealed record AssignmentHistoryDetails(Guid Id, long Sequence, Guid OperationId, Guid LocationId,
    string LocationName, DateTimeOffset ChangedAt, HistoryIdentityDetails Actor, HistoryOwnerDetails? PreviousOwner,
    HistoryOwnerDetails? NewOwner, OwnershipChangeCause Cause, string? Reason, string Display);

// Current actor and team authority always come from server-side staff data.
public sealed record AddTerritoryAssignmentRequest(string? RepSubject, TerritoryTarget? Target, string? Reason = null, string? PreviewProof = null, bool Confirmed = false);
public sealed record RemoveTerritoryAssignmentRequest(string? Version, string? Reason = null, string? PreviewProof = null, bool Confirmed = false);
public sealed record SetRepReportingLineRequest(string? ManagerSubject, string? Version);
public sealed record CoverageError(string Error, string? Field = null, AssignmentImpactDetails? Preview = null);
public sealed record CoverageMutationResult(bool Saved, int ChangedLocations);
public sealed record StaffChoice(string Subject, string Name);
public sealed record NamedRepReportingLine(RepReportingLineDetails Line, string RepName, string ManagerName);
public sealed record ReportingLinesPage(IReadOnlyList<StaffChoice> Reps, IReadOnlyList<StaffChoice> Managers,
    IReadOnlyList<NamedRepReportingLine> Lines);

public sealed record ImpactOwnerDetails(StaffChoice Rep, TerritoryTarget Source, string SourceName);
public sealed record ImpactLocationDetails(Guid LocationId, string Name, ImpactOwnerDetails? PreviousOwner, ImpactOwnerDetails? NewOwner);
public sealed record AssignmentImpactGroup(string? PreviousRepSubject, string? NewRepSubject, string Sentence, IReadOnlyList<ImpactLocationDetails> Locations);
public sealed record AssignmentImpactDetails(string Proof, string Action, string TargetName, string RepName,
    int ChangedLocations, IReadOnlyList<AssignmentImpactGroup> Groups, string? AssignmentVersion = null, IReadOnlyList<string>? TransferNotices = null, IReadOnlyList<string>? TransferredAssignments = null);
public sealed record CoverageAssignmentHolder(Guid AssignmentId, StaffChoice Rep);
public sealed record CoverageTargetChoice(TerritoryTarget Target, string Name, string Label, CoverageAssignmentHolder? Holder = null);
public sealed record AssignmentReviewOptions(IReadOnlyList<StaffChoice> Reps, IReadOnlyList<CoverageTargetChoice> Targets,
    IReadOnlyList<AssignedTerritoryDetails> Assignments);
public sealed record TransferSourceOptions(StaffChoice ReceivingRep, IReadOnlyList<StaffChoice> GivingReps);

public sealed record TerritoryCarveOut(StaffChoice Rep, int Locations);
public sealed record TerritoryTown(Guid Id, string Name, bool Archived, int Locations,
    StaffChoice? AssignedTo, IReadOnlyList<TerritoryCarveOut> CarveOuts);
public sealed record RepTerritoryAssignment(AssignedTerritoryDetails Assignment, string Context, bool Archived,
    int Locations, IReadOnlyList<TerritoryCarveOut> CarveOuts, IReadOnlyList<TerritoryTown> Towns);
public sealed record RepTerritoryPage(StaffChoice Rep, StaffChoice? Manager, int PrimaryLocations,
    IReadOnlyList<RepTerritoryAssignment> Assignments);

public sealed record TransferSelection(Guid AssignmentId, TerritoryTarget Target);
public sealed record TransferAssignmentsRequest(string? SourceRepSubject, string? ReceivingRepSubject,
    IReadOnlyList<TransferSelection>? Selections, string? Reason = null, string? PreviewProof = null, bool Confirmed = false);
public sealed record TransferScope(TransferSelection Selection, string Name, bool Archived, int Locations,
    StaffChoice? AssignedTo, IReadOnlyList<TransferScope> Children, string Context = "");
public sealed record TransferReview(StaffChoice SourceRep, IReadOnlyList<StaffChoice> ReceivingReps, IReadOnlyList<TransferScope> Assignments);
