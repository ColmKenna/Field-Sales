namespace FieldSales.Api.Directory;

// Decimal degrees, normalized to the same precision used in SQL storage.
public sealed record Coordinates
{
    public decimal Latitude { get; }
    public decimal Longitude { get; }

    public static Coordinates? FromPair(decimal? latitude, decimal? longitude)
    {
        if (latitude is null && longitude is null) return null;
        if (latitude is null || longitude is null)
            throw new CustomerDirectoryValidationException("Position", "Enter both latitude and longitude, or leave both blank.");
        return new(latitude.Value, longitude.Value);
    }

    public Coordinates(decimal latitude, decimal longitude)
    {
        if (latitude is < -90m or > 90m)
            throw new CustomerDirectoryValidationException("Latitude", "Enter a latitude between -90 and 90.");
        if (longitude is < -180m or > 180m)
            throw new CustomerDirectoryValidationException("Longitude", "Enter a longitude between -180 and 180.");
        Latitude = decimal.Round(latitude, 7, MidpointRounding.AwayFromZero);
        Longitude = decimal.Round(longitude, 7, MidpointRounding.AwayFromZero);
    }
}
