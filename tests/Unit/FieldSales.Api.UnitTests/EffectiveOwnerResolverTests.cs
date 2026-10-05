using FieldSales.Api.Coverage;
using FieldSales.Directory.Contracts;

namespace FieldSales.Api.Tests;

[Trait("Category", "Unit")]

public sealed class EffectiveOwnerResolverTests
{
    [Fact]
    public void Should_ResolveColmViaWicklow_When_CountyWith140LocationsIsAssigned()
    {
        var county = new CountyLocations("Wicklow", 140, 23);
        var assignment = TerritoryAssignment.Create("colm", new(TerritoryLevel.County, county.CountyId));
        var resolver = new EffectiveOwnerResolver([assignment]);

        Assert.Equal(140, county.Locations.Length);
        Assert.All(county.Locations, location => AssertOwner(resolver.Resolve(location), assignment, "Wicklow"));
    }

    [Fact]
    public void Should_ResolveAoifeViaRathdrum_When_TownCarvesOutColmsCounty()
    {
        var county = new CountyLocations("Wicklow", 140, 23);
        var colm = TerritoryAssignment.Create("colm", new(TerritoryLevel.County, county.CountyId));
        var aoife = TerritoryAssignment.Create("aoife", new(TerritoryLevel.Town, county.CarvedTownId));
        // Assignment order never determines precedence.
        foreach (var assignments in new[] { new[] { colm, aoife }, new[] { aoife, colm } })
        {
            var resolver = new EffectiveOwnerResolver(assignments);
            Assert.Equal(23, county.CarvedLocations.Length);
            Assert.All(county.CarvedLocations, location => AssertOwner(resolver.Resolve(location), aoife, "Rathdrum"));
            Assert.All(county.OtherLocations, location => AssertOwner(resolver.Resolve(location), colm, "Wicklow"));
            AssertOwner(resolver.Resolve(county.Locations.Single(item => item.LocationName == "Murphy's Pharmacy")), aoife, "Rathdrum");
            AssertOwner(resolver.Resolve(county.Locations.Single(item => item.LocationName == "Doyle's Shop")), colm, "Wicklow");
        }
    }

    [Fact]
    public void Should_Return23LocationsToColm_When_RathdrumAssignmentIsRemoved()
    {
        var county = new CountyLocations("Wicklow", 140, 23);
        var colm = TerritoryAssignment.Create("colm", new(TerritoryLevel.County, county.CountyId));
        var aoife = TerritoryAssignment.Create("aoife", new(TerritoryLevel.Town, county.CarvedTownId));
        List<TerritoryAssignment> assignments = [colm, aoife];
        var before = new EffectiveOwnerResolver(assignments);
        assignments.Remove(aoife);
        var after = new EffectiveOwnerResolver(assignments);

        Assert.All(county.CarvedLocations, location =>
        {
            AssertOwner(before.Resolve(location), aoife, "Rathdrum");
            AssertOwner(after.Resolve(location), colm, "Wicklow");
        });
        Assert.Equal(23, county.Locations.Count(location => before.Resolve(location)!.RepSubject != after.Resolve(location)!.RepSubject));
        Assert.All(county.OtherLocations, location => Assert.Equal(before.Resolve(location), after.Resolve(location)));
    }

    [Fact]
    public void Should_Leave96LocationsUnassigned_When_BriansOnlyWexfordAssignmentIsRemoved()
    {
        var county = new CountyLocations("Wexford", 96, 0);
        var brian = TerritoryAssignment.Create("brian", new(TerritoryLevel.County, county.CountyId));
        var before = new EffectiveOwnerResolver([brian]);
        var after = new EffectiveOwnerResolver([]);

        Assert.Equal(96, county.Locations.Length);
        Assert.All(county.Locations, location =>
        {
            AssertOwner(before.Resolve(location), brian, "Wexford");
            Assert.Null(after.Resolve(location));
        });
    }

    [Fact]
    public void Should_PreserveCarveOutOwners_When_ParentCountyAssignmentIsRemoved()
    {
        var county = new CountyLocations("Wicklow", 140, 23);
        var colm = TerritoryAssignment.Create("colm", new(TerritoryLevel.County, county.CountyId));
        var aoife = TerritoryAssignment.Create("aoife", new(TerritoryLevel.Town, county.CarvedTownId));
        var before = new EffectiveOwnerResolver([colm, aoife]);
        var after = new EffectiveOwnerResolver([aoife]);

        Assert.All(county.CarvedLocations, location => Assert.Equal(before.Resolve(location), after.Resolve(location)));
        Assert.All(county.OtherLocations, location => Assert.Null(after.Resolve(location)));
        Assert.Equal(23, county.Locations.Count(location => after.Resolve(location) is not null));
        Assert.Equal(117, county.Locations.Count(location => after.Resolve(location) is null));
    }

    [Fact]
    public void Should_PreferLocationOverTown_When_BothAssignmentsApply() =>
        AssertPair(TerritoryLevel.Location, TerritoryLevel.Town);

    [Fact]
    public void Should_PreferTownOverCounty_When_BothAssignmentsApply() =>
        AssertPair(TerritoryLevel.Town, TerritoryLevel.County);

    [Fact]
    public void Should_PreferCountyOverRegion_When_BothAssignmentsApply() =>
        AssertPair(TerritoryLevel.County, TerritoryLevel.Region);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Should_PreferLocation_When_AllFourLevelsApply(bool sameRep)
    {
        // Equal GUIDs across different levels are valid (covered by the schema
        // tests); the resolver must key by level as well as unit identity.
        Guid unit = Guid.NewGuid();
        var path = new LocationOwnershipPath(unit, "Murphy's Pharmacy", unit, "Rathdrum", unit, "Wicklow", unit, "Leinster");
        var assignments = Enum.GetValues<TerritoryLevel>().Select(level =>
            TerritoryAssignment.Create(sameRep ? "colm" : level.ToString(), new(level, unit))).ToList();

        foreach (var level in Enum.GetValues<TerritoryLevel>().Reverse())
        {
            var expected = assignments.Single(item => item.Target.Level == level);
            AssertOwner(new EffectiveOwnerResolver(assignments).Resolve(path), expected, SourceName(path, level));
            AssertOwner(new EffectiveOwnerResolver(assignments.AsEnumerable().Reverse()).Resolve(path), expected, SourceName(path, level));
            assignments.Remove(expected);
        }
        Assert.Null(new EffectiveOwnerResolver(assignments).Resolve(path));
    }

    [Fact]
    public void Should_ResolveUnassigned_When_NoAssignmentApplies() =>
        Assert.Null(new EffectiveOwnerResolver([]).Resolve(Path()));

    [Theory]
    [InlineData(TerritoryLevel.Region)]
    [InlineData(TerritoryLevel.County)]
    [InlineData(TerritoryLevel.Town)]
    [InlineData(TerritoryLevel.Location)]
    public void Should_ResolveUnassigned_When_AssignmentsApplyOnlyToOtherUnits(TerritoryLevel level)
    {
        var assignment = TerritoryAssignment.Create("other-rep", new(level, Guid.NewGuid()));
        Assert.Null(new EffectiveOwnerResolver([assignment]).Resolve(Path()));
    }

    [Theory]
    [InlineData(TerritoryLevel.Region)]
    [InlineData(TerritoryLevel.County)]
    [InlineData(TerritoryLevel.Town)]
    [InlineData(TerritoryLevel.Location)]
    public void Should_RejectDuplicateAssignment_When_SameLevelUnitHasTwoOwners(TerritoryLevel level)
    {
        Guid unit = Guid.NewGuid();
        var colm = TerritoryAssignment.Create("colm", new(level, unit));
        var aoife = TerritoryAssignment.Create("aoife", new(level, unit));
        Assert.Throws<InvalidOperationException>(() => new EffectiveOwnerResolver([colm, aoife]));
    }

    private static void AssertPair(TerritoryLevel specific, TerritoryLevel broad)
    {
        var path = Path();
        var narrower = TerritoryAssignment.Create("aoife", Target(path, specific));
        var wider = TerritoryAssignment.Create("colm", Target(path, broad));
        AssertOwner(new EffectiveOwnerResolver([wider, narrower]).Resolve(path), narrower, SourceName(path, specific));
        AssertOwner(new EffectiveOwnerResolver([narrower, wider]).Resolve(path), narrower, SourceName(path, specific));
        AssertOwner(new EffectiveOwnerResolver([wider]).Resolve(path), wider, SourceName(path, broad));
    }

    private static LocationOwnershipPath Path() => new(Guid.NewGuid(), "Murphy's Pharmacy",
        Guid.NewGuid(), "Rathdrum", Guid.NewGuid(), "Wicklow", Guid.NewGuid(), "Leinster");

    private static TerritoryTarget Target(LocationOwnershipPath path, TerritoryLevel level) => new(level, level switch
    {
        TerritoryLevel.Region => path.RegionId,
        TerritoryLevel.County => path.CountyId,
        TerritoryLevel.Town => path.TownId,
        _ => path.LocationId
    });

    private static string SourceName(LocationOwnershipPath path, TerritoryLevel level) => level switch
    {
        TerritoryLevel.Region => path.RegionName,
        TerritoryLevel.County => path.CountyName,
        TerritoryLevel.Town => path.TownName,
        _ => path.LocationName
    };

    private static void AssertOwner(EffectiveOwner? actual, TerritoryAssignment assignment, string name)
    {
        Assert.NotNull(actual);
        Assert.Equal(assignment.RepSubject, actual.RepSubject);
        Assert.Equal(assignment.Id, actual.Source.AssignmentId);
        Assert.Equal(assignment.Target, actual.Source.Target);
        Assert.Equal(name, actual.Source.Name);
    }

    private sealed class CountyLocations
    {
        public Guid CountyId { get; } = Guid.NewGuid();
        public Guid CarvedTownId { get; } = Guid.NewGuid();
        public LocationOwnershipPath[] Locations { get; }
        public LocationOwnershipPath[] CarvedLocations => Locations.Where(item => item.TownId == CarvedTownId).ToArray();
        public LocationOwnershipPath[] OtherLocations => Locations.Where(item => item.TownId != CarvedTownId).ToArray();

        public CountyLocations(string name, int count, int carvedCount)
        {
            Guid otherTown = Guid.NewGuid(), region = Guid.NewGuid();
            Locations = Enumerable.Range(0, count).Select(index => new LocationOwnershipPath(
                Guid.NewGuid(), index == 0 && carvedCount > 0 ? "Murphy's Pharmacy" : index == carvedCount ? "Doyle's Shop" : "Shop " + index,
                index < carvedCount ? CarvedTownId : otherTown, index < carvedCount ? "Rathdrum" : "Laragh",
                CountyId, name, region, "Leinster")).ToArray();
        }
    }
}
