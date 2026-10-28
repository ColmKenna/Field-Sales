using FieldSales.Snapshot.Contracts;

namespace FieldSales.Tablet.Core.Tests;

public sealed class LocationSearchTests : IDisposable
{
    private readonly TestTablet tablet = new();
    public void Dispose() => tablet.Dispose();

    private async Task<LocationSearchIndex> IndexAsync(bool curlyStoreApostrophe = true)
    {
        var snapshot = Snapshots.Build("colm", 312, 0);
        var locations = snapshot.Locations.ToArray();
        locations[0] = locations[0] with { Name = "Murphy's Pharmacy", Town = locations[0].Town with { Name = "Rathdrum" } };
        locations[1] = locations[1] with { Name = curlyStoreApostrophe ? "Murphy’s Stores" : "Murphy's Stores", Town = locations[1].Town with { Name = "Aughrim" } };
        locations[2] = locations[2] with { Name = "Café de Santé" };
        locations[3] = locations[3] with { Name = "100%_Shop" };
        await tablet.SyncAsync(snapshot with { Locations = locations });
        tablet.Server.Respond = _ => throw new InvalidOperationException("Search must stay offline");
        return new SnapshotStore(tablet.Workspace()).ReadLocationIndex();
    }

    [Fact]
    public async Task Should_ShowBothMurphyShops_When_Searching312AssignedLocationsOffline()
    {
        var index = await IndexAsync(curlyStoreApostrophe: false);
        Assert.Equal(312, index.Count);
        var matches = index.Search("Murphy");
        Assert.Equal(["Murphy's Pharmacy", "Murphy's Stores"], matches.Select(row => row.Name));
        Assert.Equal(["Rathdrum", "Aughrim"], matches.Select(row => row.TownName));
        Assert.Equal(matches[0], index.Find(matches[0].Id));
        Assert.Null(index.Find(Guid.NewGuid()));
        Assert.Empty(index.Search("Quinn"));
    }

    [Theory]
    [InlineData("  pHaRm  ", "Murphy's Pharmacy")]
    [InlineData("Murphy's", "Murphy's Pharmacy", "Murphy’s Stores")]
    [InlineData("Murphy’s", "Murphy's Pharmacy", "Murphy’s Stores")]
    [InlineData("cafe", "Café de Santé")]
    [InlineData("CAFÉ", "Café de Santé")]
    [InlineData("cafe\u0301", "Café de Santé")]
    [InlineData("sante", "Café de Santé")]
    [InlineData("%_", "100%_Shop")]
    public async Task Should_MatchLiteralPartialNames_When_QueryUsesApprovedNormalization(string query, params string[] names)
    {
        var index = await IndexAsync();
        Assert.Equal(names, index.Search(query).Select(row => row.Name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \t\n")]
    public async Task Should_ClearResultsWithoutSearching_When_QueryIsBlank(string? query)
    {
        var index = await IndexAsync();
        Assert.Equal(2, index.Search("Murphy").Count);
        Assert.Empty(index.Search(query));
    }

    [Fact]
    public async Task Should_ReplaceSearchResults_When_TheActiveSnapshotChanges()
    {
        var original = await IndexAsync();
        var snapshot = Snapshots.Build("colm", 500, 0, label: "B");
        await tablet.SyncAsync(snapshot);
        var current = new SnapshotStore(tablet.Workspace()).ReadLocationIndex();
        Assert.NotEqual(original.Generation, current.Generation);
        Assert.Empty(current.Search("Murphy"));
        Assert.Equal(500, current.Search("shop").Count);
        var otherRep = new SnapshotStore(tablet.Workspace("aoife")).ReadLocationIndex();
        Assert.Null(otherRep.Generation);
        Assert.Empty(otherRep.Search("shop"));
        // A retained immutable copy stays internally consistent; callers replace it on entry/resume.
        Assert.Equal(2, original.Search("Murphy").Count);
    }
}
