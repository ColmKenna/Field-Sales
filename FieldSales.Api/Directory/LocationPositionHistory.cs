using FieldSales.Directory.Contracts;

namespace FieldSales.Api.Directory;

// A snapshot of a replaced pin, including its original source rather than the
// Location's current address. Appended in the same SaveChanges as its replacement.
public sealed class LocationPositionHistory
{
    private LocationPositionHistory() { }
    public Guid Id { get; private set; }
    public Guid LocationId { get; private set; }
    public decimal Latitude { get; private set; }
    public decimal Longitude { get; private set; }
    public LocationPositionPrecision Precision { get; private set; }
    public Guid? SourceTownId { get; private set; }
    public string? SourceEircode { get; private set; }
    public DateTimeOffset PositionedAt { get; private set; }
    public DateTimeOffset ReplacedAt { get; private set; }

    internal static LocationPositionHistory Capture(Location location, DateTimeOffset replacedAt) => new()
    {
        Id = Guid.NewGuid(), LocationId = location.Id,
        Latitude = location.Latitude!.Value, Longitude = location.Longitude!.Value,
        Precision = location.PositionPrecision!.Value, SourceTownId = location.PositionSourceTownId,
        SourceEircode = location.PositionSourceEircode, PositionedAt = location.PositionedAt!.Value,
        ReplacedAt = replacedAt
    };
}
