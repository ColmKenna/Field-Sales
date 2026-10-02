namespace FieldSales.Directory.Contracts;

public sealed record GeographyItem(Guid Id, string Name, Guid? ParentId, string Version);
public sealed record GeographyPath(Guid Id, string Name, string Level);
public sealed record GeographyPage(string Level, Guid? ParentId,
    IReadOnlyList<GeographyPath> Path, IReadOnlyList<GeographyItem> Items);
public sealed record TownChoice(Guid Id, string Name, Guid CountyId, string CountyName,
    Guid RegionId, string RegionName, string Label);
public sealed record CreateGeographyRequest(string? Name, Guid? ParentId = null);
public sealed record RenameGeographyRequest(string? Name, string? Version);
public sealed record GeographyError(string Error);
public sealed record GeographyImportResult(int RegionsAdded, int CountiesAdded, int TownsAdded, int TownsAlreadyPresent);

public static class GeographyImportLimits
{
    public const int MaximumBytes = 1024 * 1024;
    public const int MaximumRows = 10_000;
}
