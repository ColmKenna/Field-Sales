using FieldSales.Api.Directory;
using FieldSales.Directory.Contracts;

namespace FieldSales.Api.Tests;

public sealed class GeographyCsvTests
{
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
