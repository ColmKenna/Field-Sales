using FieldSales.Api.Directory;

namespace FieldSales.Api.Tests;

public sealed class CoordinatesTests
{
    [Theory]
    [InlineData(-90, -180)]
    [InlineData(0, 0)]
    [InlineData(90, 180)]
    public void Should_AcceptValidCoordinateBoundaries_IncludingZero(decimal latitude, decimal longitude)
    {
        var coordinates = new Coordinates(latitude, longitude);
        Assert.Equal(latitude, coordinates.Latitude); Assert.Equal(longitude, coordinates.Longitude);
    }

    [Theory]
    [InlineData("90.00000001", "0", "Latitude")]
    [InlineData("-90.00000001", "0", "Latitude")]
    [InlineData("0", "180.00000001", "Longitude")]
    [InlineData("0", "-180.00000001", "Longitude")]
    public void Should_RejectOutOfRangeCoordinates_BeforeRounding(string latitude, string longitude, string field)
    {
        var error = Assert.Throws<CustomerDirectoryValidationException>(() => new Coordinates(
            decimal.Parse(latitude, System.Globalization.CultureInfo.InvariantCulture),
            decimal.Parse(longitude, System.Globalization.CultureInfo.InvariantCulture)));
        Assert.Equal(field, error.Field);
    }
}
