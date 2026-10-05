using FieldSales.Api.Directory;
using FieldSales.Directory.Contracts;

namespace FieldSales.Api.Tests;

public sealed class GeographyCsvTests
{
    [Fact]
    public void Should_ReadOptionalCoordinateColumns_WithoutChangingLegacyRows()
    {
        var rows = GeographyCsv.Parse("Region,County,Town,Latitude,Longitude\nLeinster,Wicklow,Laragh,52.923456789,-6.291234567\nLeinster,Wicklow,Rathdrum,,\nLeinster,Wicklow,Arklow,0,0");
        Assert.Equal(new Coordinates(52.9234568m, -6.2912346m), rows[0].Coordinates);
        Assert.Null(rows[1].Coordinates);
        Assert.Equal(new Coordinates(0, 0), rows[2].Coordinates);
        Assert.Null(Assert.Single(GeographyCsv.Parse("Region,County,Town\nLeinster,Wicklow,Laragh")).Coordinates);
    }

    [Theory]
    [InlineData("52,", "both latitude and longitude")]
    [InlineData(",-6", "both latitude and longitude")]
    [InlineData("90.00000001,-6", "latitude between")]
    [InlineData("52,-180.00000001", "longitude between")]
    [InlineData("NaN,-6", "decimal number")]
    [InlineData("Infinity,-6", "decimal number")]
    [InlineData("\"52,9\",-6", "decimal number")]
    [InlineData("52,-6,extra", "5 columns")]
    public void Should_RejectInvalidTownCoordinates_WithTheCsvRowNumber(string values, string error)
    {
        var exception = Assert.Throws<GeographyValidationException>(() => GeographyCsv.Parse(
            "Region,County,Town,Latitude,Longitude\nLeinster,Wicklow,Laragh," + values));
        Assert.Contains("Row 2", exception.Message); Assert.Contains(error, exception.Message);
    }

    [Fact]
    public void Should_LoadHierarchy_When_FileIsValid()
    {
        var rows = GeographyCsv.Parse("\uFEFFRegion,County,Town\r\n\r\nLeinster,Wicklow,Rathdrum\r\nConnacht,Galway,\"Béal, Átha\"\r\nLeinster,Wicklow,\"Town \"\"East\"\"\"\n");
        Assert.Equal(3, rows.Count);
        Assert.Equal(new GeographySeedRow("Leinster", "Wicklow", "Rathdrum"), rows[0]);
        Assert.Equal("Béal, Átha", rows[1].Town);
        Assert.Equal("Town \"East\"", rows[2].Town);
    }

    [Theory]
    [InlineData("", "no towns")]
    [InlineData("Region,County,Town\n", "no towns")]
    [InlineData("County,Region,Town\nWicklow,Leinster,Rathdrum", "header")]
    [InlineData("Region,County,Town\nLeinster,Wicklow", "Row 2")]
    [InlineData("Region,County,Town\nLeinster,Wicklow,Town,Extra", "Row 2")]
    [InlineData("Region,County,Town\nLeinster,,Rathdrum", "Row 2")]
    [InlineData("Region,County,Town\nLeinster,Wicklow,\"Town", "Row 2")]
    [InlineData("Region,County,Town\nLeinster,Wicklow,To\"wn", "Row 2")]
    [InlineData("Region,County,Town\nLeinster,Wicklow,\"Town\"x", "Row 2")]
    [InlineData("Region,County,Town\nLeinster,Wicklow,\"Town\nEast\"", "Row 2")]
    public void Should_SaveNothing_When_ImportContainsInvalidRows(string text, string error)
    {
        Assert.Contains(error, Assert.Throws<GeographyValidationException>(() => GeographyCsv.Parse(text)).Message);
    }

    [Fact]
    public void Should_SaveNothing_When_ImportExceedsResourceBounds()
    {
        Assert.Contains("1 MiB", Assert.Throws<GeographyValidationException>(() =>
            GeographyCsv.Parse(new string('é', GeographyImportLimits.MaximumBytes / 2 + 1))).Message);
        Assert.Contains("10,000", Assert.Throws<GeographyValidationException>(() =>
            GeographyCsv.Parse("Region,County,Town\n" + string.Concat(Enumerable.Repeat("Leinster,Wicklow,Rathdrum\n", 10_001)))).Message);
    }
}
