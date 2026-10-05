namespace FieldSales.Api.Directory;

public abstract class GeographyEntity
{
    public Guid Id { get; protected set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public byte[] Version { get; private set; } = [];
    public bool IsArchived { get; private set; }

    internal void Archive() => IsArchived = true;
    internal void Unarchive() => IsArchived = false;

    internal void Rename(string name)
    {
        Name = GeographyNames.Validate(name);
        NormalizedName = Name.ToUpperInvariant();
    }
}

public sealed class Region : GeographyEntity
{
    private Region() { }
    internal Region(string name) { Id = Guid.NewGuid(); Rename(name); }
}

public sealed class County : GeographyEntity
{
    private County() { }
    internal County(Guid regionId, string name) { Id = Guid.NewGuid(); RegionId = regionId; Rename(name); }
    public Guid RegionId { get; private set; }
}

public sealed class Town : GeographyEntity
{
    private Town() { }
    internal Town(Guid countyId, string name) { Id = Guid.NewGuid(); CountyId = countyId; Rename(name); }
    public Guid CountyId { get; private set; }
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }

    internal bool SetCoordinates(decimal? latitude, decimal? longitude)
    {
        Coordinates? coordinates;
        try { coordinates = Coordinates.FromPair(latitude, longitude); }
        catch (CustomerDirectoryValidationException exception) { throw new GeographyValidationException(exception.Message); }
        if (Latitude == coordinates?.Latitude && Longitude == coordinates?.Longitude) return false;
        Latitude = coordinates?.Latitude;
        Longitude = coordinates?.Longitude;
        return true;
    }
}

public static class GeographyNames
{
    public static string Validate(string? name)
    {
        string trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length is 0 or > 200 || trimmed.Any(char.IsControl))
            throw new GeographyValidationException("Enter a name of up to 200 characters.");
        return trimmed;
    }
}

public sealed class GeographyValidationException(string message) : Exception(message);
