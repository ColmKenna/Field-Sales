namespace FieldSales.Directory.Contracts;

public enum LocationPositionPrecision { Town = 0, Eircode = 1, ConfirmedOnSite = 2 }

public sealed record LocationPosition(decimal Latitude, decimal Longitude, LocationPositionPrecision Precision,
    DateTimeOffset PositionedAt);
