using FieldSales.Directory.Contracts;

namespace FieldSales.Api.Coverage;

public sealed class TerritoryAssignment
{
    private TerritoryAssignment() { }

    public Guid Id { get; private set; }
    public string RepSubject { get; private set; } = string.Empty;
    public Guid? RegionId { get; private set; }
    public Guid? CountyId { get; private set; }
    public Guid? TownId { get; private set; }
    public Guid? LocationId { get; private set; }
    public byte[] Version { get; private set; } = [];

    // Level and unit are derived from the single typed target, never stored twice.
    public TerritoryTarget Target => RegionId is Guid region ? new(TerritoryLevel.Region, region)
        : CountyId is Guid county ? new(TerritoryLevel.County, county)
        : TownId is Guid town ? new(TerritoryLevel.Town, town)
        : LocationId is Guid location ? new(TerritoryLevel.Location, location)
        : throw new InvalidOperationException("The assignment has no target.");

    public void TransferTo(string repSubject) => RepSubject = CoverageSubjects.Validate(repSubject, "ReceivingRepSubject");

    internal TerritoryAssignment CopyForTransfer(string repSubject) => new()
    {
        Id = Id, RepSubject = CoverageSubjects.Validate(repSubject, "ReceivingRepSubject"),
        RegionId = RegionId, CountyId = CountyId, TownId = TownId, LocationId = LocationId, Version = Version.ToArray()
    };

    public static TerritoryAssignment Create(string? repSubject, TerritoryTarget target) => CreateForTransfer(Guid.NewGuid(), repSubject, target);

    internal static TerritoryAssignment CreateForTransfer(Guid id, string? repSubject, TerritoryTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (!Enum.IsDefined(target.Level))
            throw new CoverageValidationException("Target.Level", "Choose a territory level.");
        if (target.UnitId == Guid.Empty)
            throw new CoverageValidationException("Target.UnitId", "Choose a territory or location.");
        var assignment = new TerritoryAssignment
        {
            Id = id, RepSubject = CoverageSubjects.Validate(repSubject, "RepSubject")
        };
        switch (target.Level)
        {
            case TerritoryLevel.Region: assignment.RegionId = target.UnitId; break;
            case TerritoryLevel.County: assignment.CountyId = target.UnitId; break;
            case TerritoryLevel.Town: assignment.TownId = target.UnitId; break;
            case TerritoryLevel.Location: assignment.LocationId = target.UnitId; break;
        }
        return assignment;
    }
}
