using FieldSales.Directory.Contracts;

namespace FieldSales.Web.Tests;

internal static class LocationCoverageClientTests
{
    internal static AssignmentHistoryDetails Entry(Guid location, long sequence)
    {
        var source = new CoverageSourceDetails(Guid.NewGuid(), new(TerritoryLevel.Town, Guid.NewGuid()), "Rathdrum");
        return new(Guid.NewGuid(), sequence, Guid.NewGuid(), location, "Murphy's Pharmacy",
            new(2026, 9, 17, 14, 2, 0, TimeSpan.Zero), new("niamh", "M. Byrne"),
            new(new("colm", "Colm"), source), new(new("aoife", "Aoife"), source), OwnershipChangeCause.TerritoryAssignment, null, "Captured original display");
    }
}
