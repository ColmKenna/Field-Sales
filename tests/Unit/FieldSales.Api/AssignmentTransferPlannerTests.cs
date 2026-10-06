using FieldSales.Api.Coverage;
using FieldSales.Directory.Contracts;

namespace FieldSales.Api.Tests;

[Trait("Category", "Unit")]

public sealed class AssignmentTransferPlannerTests
{
    private readonly List<TransferUnit> geography = [];
    private TerritoryTarget Unit(TerritoryLevel level, string name, TerritoryTarget? parent = null, bool archived = false)
    { var target = new TerritoryTarget(level, Guid.NewGuid()); geography.Add(new(target, parent, name, archived)); return target; }
    private AssignmentTransferPlan Plan(TerritoryAssignment[] rows, string recipient, params TransferSelection[] selected) =>
        AssignmentTransferPlanner.Calculate(geography, rows, "colm", recipient, selected, recipient == "ciara" ? "Ciara" : "Niamh");
    private static string Owner(AssignmentTransferPlan plan, TerritoryTarget target) => Assert.Single(plan.Assignments, row => row.Target == target).RepSubject;

    [Fact]
    public void P1_WholeCountyPreservesCarveOutsAndIdentity()
    {
        var county = Unit(TerritoryLevel.County, "Wicklow"); var town = Unit(TerritoryLevel.Town, "Rathdrum", county);
        var a = TerritoryAssignment.Create("colm", county); var b = TerritoryAssignment.Create("aoife", town);
        var result = Plan([a, b], "niamh", new TransferSelection(a.Id, county));
        Assert.Equal("niamh", Owner(result, county)); Assert.Equal("aoife", Owner(result, town));
        Assert.Equal(a.Id, result.Assignments.Single(row => row.Target == county).Id); Assert.Equal(2, result.Assignments.Count);
        Assert.Equal("colm", a.RepSubject); Assert.Empty(result.Notices);
    }

    [Fact]
    public void P2_P3_PartialThenLastTownsProduceExactAssignmentSets()
    {
        var county = Unit(TerritoryLevel.County, "Wicklow");
        var towns = new[] { "Bray", "Greystones", "Wicklow Town", "Arklow", "Aughrim", "Rathdrum" }.Select(name => Unit(TerritoryLevel.Town, name, county)).ToArray();
        var a = TerritoryAssignment.Create("colm", county); var carve = TerritoryAssignment.Create("aoife", towns[5]);
        var first = Plan([a, carve], "niamh", towns.Take(3).Select(t => new TransferSelection(a.Id, t)).ToArray());
        Assert.Equal(5, first.Assignments.Count); Assert.Equal("colm", Owner(first, county));
        foreach (var town in towns.Take(3)) Assert.Equal("niamh", Owner(first, town));
        Assert.DoesNotContain(first.Assignments, row => row.Target == towns[3] || row.Target == towns[4]);
        var last = Plan(first.Assignments.ToArray(), "ciara", new TransferSelection(a.Id, towns[3]), new TransferSelection(a.Id, towns[4]));
        Assert.Equal(5, last.Assignments.Count); Assert.Equal("ciara", Owner(last, county)); Assert.Equal("aoife", Owner(last, towns[5]));
        foreach (var town in towns.Take(3)) Assert.Equal("niamh", Owner(last, town));
        Assert.Equal("Wicklow (County) moves to Ciara with its last Towns.", Assert.Single(last.Notices));
        Assert.Equal(first.Assignments.Select(r => r.Id).Order(), last.Assignments.Select(r => r.Id).Order());
        var future = new LocationOwnershipPath(Guid.NewGuid(), "Future", Guid.NewGuid(), "New Town", county.UnitId, "Wicklow", Guid.NewGuid(), "Region");
        Assert.Equal("ciara", new EffectiveOwnerResolver(last.Assignments).Resolve(future)!.RepSubject);
    }

    [Fact]
    public void P4_PartialRegionThenLastCountyMovesRegion()
    {
        var region = Unit(TerritoryLevel.Region, "South East"); var wicklow = Unit(TerritoryLevel.County, "Wicklow", region); var wexford = Unit(TerritoryLevel.County, "Wexford", region);
        var a = TerritoryAssignment.Create("colm", region);
        var first = Plan([a], "ciara", new TransferSelection(a.Id, wicklow)); Assert.Equal("colm", Owner(first, region)); Assert.Equal("ciara", Owner(first, wicklow));
        var last = Plan(first.Assignments.ToArray(), "ciara", new TransferSelection(a.Id, wexford));
        Assert.Equal(2, last.Assignments.Count); Assert.Equal("ciara", Owner(last, region)); Assert.Equal("ciara", Owner(last, wicklow));
        Assert.Equal("South East (Region) moves to Ciara with its last Counties.", Assert.Single(last.Notices));
    }

    [Theory]
    [InlineData(TerritoryLevel.County, TerritoryLevel.Town)] [InlineData(TerritoryLevel.Region, TerritoryLevel.County)]
    public void P5_P7_ExcludedEmptyArchivedChildPreventsRollUp(TerritoryLevel level, TerritoryLevel childLevel)
    {
        var parent = Unit(level, "Parent"); var selected = Unit(childLevel, "Selected", parent); var excluded = Unit(childLevel, "Empty archived", parent, true);
        var a = TerritoryAssignment.Create("colm", parent);
        var result = Plan([a], "niamh", new TransferSelection(a.Id, selected));
        Assert.Equal("colm", Owner(result, parent)); Assert.Equal("niamh", Owner(result, selected));
        Assert.DoesNotContain(result.Assignments, row => row.Target == excluded); Assert.Empty(result.Notices);
        var all = Plan([a], "niamh", new TransferSelection(a.Id, selected), new TransferSelection(a.Id, excluded));
        Assert.Single(all.Assignments); Assert.Equal("niamh", Owner(all, parent)); Assert.Single(all.Notices);
        Assert.True(geography.Single(row => row.Target == excluded).Archived);
    }

    [Fact]
    public void P6_OverlapsAndSeparateNarrowerSelectionsPreserveUnselectedRows()
    {
        var county = Unit(TerritoryLevel.County, "County"); var inherited = Unit(TerritoryLevel.Town, "Inherited", county);
        var own = Unit(TerritoryLevel.Town, "Own", county); var foreign = Unit(TerritoryLevel.Town, "Foreign", county);
        var location = Unit(TerritoryLevel.Location, "Direct", inherited);
        var a = TerritoryAssignment.Create("colm", county); var b = TerritoryAssignment.Create("colm", own);
        var c = TerritoryAssignment.Create("aoife", foreign); var d = TerritoryAssignment.Create("colm", location);
        var result = Plan([a,b,c,d], "niamh", new TransferSelection(a.Id,county),new TransferSelection(a.Id,inherited),new TransferSelection(a.Id,inherited),new TransferSelection(b.Id,own));
        Assert.Equal(4,result.Assignments.Count); Assert.Equal("niamh",Owner(result,county)); Assert.Equal("niamh",Owner(result,own));
        Assert.Equal("aoife",Owner(result,foreign)); Assert.Equal("colm",Owner(result,location));
        Assert.Equal("colm",a.RepSubject); Assert.Equal("colm",b.RepSubject);
    }

    [Fact]
    public void P7_EmptySelectionDoesNotRollUpEmptyParent()
    {
        var county = Unit(TerritoryLevel.County, "Empty", archived:true); var a = TerritoryAssignment.Create("colm",county);
        var unchanged = Plan([a],"niamh"); Assert.Equal("colm",Owner(unchanged,county)); Assert.Empty(unchanged.Notices);
        Assert.Equal("niamh",Owner(Plan([a],"niamh",new TransferSelection(a.Id,county)),county));
    }

    [Theory]
    [InlineData("same-rep")] [InlineData("unknown")] [InlineData("foreign")] [InlineData("wrong-parent")] [InlineData("carve-out")]
    public void P8_InvalidSelectionCannotMutateInputs(string invalid)
    {
        var county = Unit(TerritoryLevel.County,"County"); var elsewhere = Unit(TerritoryLevel.County,"Elsewhere");
        var child = Unit(TerritoryLevel.Town,"Child",county); var outside = Unit(TerritoryLevel.Town,"Outside",elsewhere);
        var a = TerritoryAssignment.Create("colm",county); var b = TerritoryAssignment.Create("aoife",child);
        TransferSelection selection = invalid switch { "unknown"=>new(Guid.NewGuid(),county),"foreign"=>new TransferSelection(b.Id,child),"wrong-parent"=>new TransferSelection(a.Id,outside),"carve-out"=>new TransferSelection(a.Id,child),_=>new TransferSelection(a.Id,county) };
        Assert.Throws<CoverageValidationException>(()=>Plan([a,b],invalid=="same-rep"?"colm":"niamh",new TransferSelection(a.Id,county),selection));
        Assert.Equal("colm",a.RepSubject); Assert.Equal("aoife",b.RepSubject);
    }
}
