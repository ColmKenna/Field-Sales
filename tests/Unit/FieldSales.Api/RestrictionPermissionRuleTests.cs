using FieldSales.Api.Coverage;
using FieldSales.StaffAccess;

namespace FieldSales.Api.Tests;

[Trait("Category", "Unit")]

public sealed class RestrictionPermissionRuleTests
{
    [Theory]
    // The rep's own Sales Manager may change; a manager of another team, or of nobody, may not.
    [InlineData("niamh", BusinessRoles.SalesManager, "colm", "niamh", true)]
    [InlineData("niamh", BusinessRoles.SalesManager, "ciara", "another-manager", false)]
    [InlineData("niamh", BusinessRoles.SalesManager, "ciara", null, false)]
    [InlineData("niamh", BusinessRoles.SalesManager, "colm", "NIAMH", false)]
    // Any Head Office User may change any rep, including one with no reporting line or a selling manager.
    [InlineData("orla", BusinessRoles.HeadOfficeUser, "ciara", "another-manager", true)]
    [InlineData("orla", BusinessRoles.HeadOfficeUser, "ciara", null, true)]
    [InlineData("orla", BusinessRoles.HeadOfficeUser, "sorcha", "another-manager", true)]
    [InlineData("niamh", BusinessRoles.SalesManager + "," + BusinessRoles.HeadOfficeUser, "ciara", "another-manager", true)]
    // Nobody changes their own permissions, Head Office included.
    [InlineData("sorcha", BusinessRoles.SalesManager + "," + BusinessRoles.FieldSalesperson, "sorcha", "sorcha", false)]
    [InlineData("orla", BusinessRoles.HeadOfficeUser + "," + BusinessRoles.FieldSalesperson, "orla", "another-manager", false)]
    // Without a manager role nobody may change, even when named as the rep's manager.
    [InlineData("niamh", BusinessRoles.FieldSalesperson, "colm", "niamh", false)]
    [InlineData("niamh", "", "colm", "niamh", false)]
    public void Should_AllowOnlyTheRepsManagerOrHeadOffice_When_DecidingWhoMayChange(
        string actor, string roles, string rep, string? repManager, bool expected)
    {
        string[] actorRoles = roles.Split(',', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(expected, RestrictionPermissionRule.CanChange(actor, actorRoles, rep, repManager));
    }
}
