using FieldSales.Api.Coverage;
using FieldSales.Directory.Contracts;

namespace FieldSales.Api.Tests;

public sealed class AssignmentImpactPreviewTests
{
    [Fact]
    public void Should_ReturnOnlyChangedRepsWithoutMutatingInputs_When_FourLevelsOverlap()
    {
        var path = new LocationOwnershipPath(Guid.NewGuid(), "Murphy's", Guid.NewGuid(), "Rathdrum", Guid.NewGuid(), "Wicklow", Guid.NewGuid(), "Leinster");
        var county = TerritoryAssignment.Create("colm", new(TerritoryLevel.County, path.CountyId));
        var town = TerritoryAssignment.Create("aoife", new(TerritoryLevel.Town, path.TownId));
        var direct = TerritoryAssignment.Create("brian", new(TerritoryLevel.Location, path.LocationId));
        TerritoryAssignment[] before = [county, town, direct];
        var change = Assert.Single(AssignmentImpactPreview.Calculate([path], before, [county, town]));
        Assert.Equal("brian", change.Previous!.RepSubject); Assert.Equal("aoife", change.Next!.RepSubject);
        Assert.Equal(TerritoryLevel.Town, change.Next.Source.Target.Level); Assert.Equal(3, before.Length);
        var sameRep = TerritoryAssignment.Create("aoife", new(TerritoryLevel.Location, path.LocationId));
        Assert.Empty(AssignmentImpactPreview.Calculate([path], [county, town], [county, town, sameRep]));
    }
    [Fact]
    public void Should_RequireFreshProof_When_PreviewExpiredOrApiRestarted()
    {
        var clock = new PreviewClock(); var proofs = new AssignmentPreviewProofs(clock);
        string proof = proofs.Issue("actor", "command", "inputs");
        Assert.True(proofs.Matches(proof, true, "actor", "command", "inputs"));
        Assert.False(proofs.Matches(proof, true, "actor", "command", "changed-inputs"));
        clock.Now += TimeSpan.FromMinutes(31);
        Assert.False(proofs.Matches(proof, true, "actor", "command", "inputs"));
        Assert.Throws<CoverageValidationException>(() => new AssignmentPreviewProofs(clock).Matches(proof, true, "actor", "command", "inputs"));
    }
    private sealed class PreviewClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
