using FieldSales.ReferenceData;

namespace FieldSales.Directory.Contracts;

public sealed record GeographyItem(Guid Id, string Name, Guid? ParentId, string Version,
    bool IsArchived = false, ReferenceUsage? Usage = null, decimal? Latitude = null, decimal? Longitude = null);
public sealed record GeographyPath(Guid Id, string Name, string Level, bool IsArchived = false);
public sealed record GeographyPage(string Level, Guid? ParentId,
    IReadOnlyList<GeographyPath> Path, IReadOnlyList<GeographyItem> Items,
    int ArchivedCount = 0, bool ShowArchived = false);
public sealed record TownChoice(Guid Id, string Name, Guid CountyId, string CountyName,
    Guid RegionId, string RegionName, string Label,
    bool IsArchived = false, bool CountyIsArchived = false, bool RegionIsArchived = false,
    decimal? Latitude = null, decimal? Longitude = null)
{
    public bool IsSelectable => !IsArchived && !CountyIsArchived && !RegionIsArchived;
}
public sealed record CreateGeographyRequest(string? Name, Guid? ParentId = null, decimal? Latitude = null, decimal? Longitude = null);
public sealed record SetTownCoordinatesRequest(decimal? Latitude, decimal? Longitude, string? Version);
public sealed record RenameGeographyRequest(string? Name, string? Version);
public sealed record RetireGeographyRequest(ReferenceAction Action, string? Version);
public sealed record GeographyMutationResult(bool Saved);
public sealed record GeographyError(string Error);
public sealed record GeographyImportResult(int RegionsAdded, int CountiesAdded, int TownsAdded, int TownsAlreadyPresent,
    int TownCoordinatesUpdated = 0);

public static class GeographyImportLimits
{
    public const int MaximumBytes = 1024 * 1024;
    public const int MaximumRows = 10_000;
}
