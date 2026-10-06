using System.Security.Claims;
using FieldSales.StaffAccess;

namespace FieldSales.Web.Tests;

[Trait("Category", "Unit")]

public sealed class StaffContractTests
{
    [Theory]
    [InlineData("fieldsales.api", true)]
    [InlineData("openid fieldsales.api profile", true)]
    [InlineData("fieldsales.api.extra", false)]
    [InlineData("prefix-fieldsales.api", false)]
    [InlineData("FIELDSALES.API", false)]
    public void Should_UseExactSpaceSeparatedValues_When_MatchingScopes(string scope, bool expected)
    {
        ClaimsPrincipal principal = new(new ClaimsIdentity([new Claim("scope", "openid"), new Claim("scope", scope)]));
        Assert.Equal(expected, principal.HasScope(StaffApiContract.Scope));
    }

    [Fact]
    public void Should_PreserveFirstSubClaimPrecedence_When_LookingUpSubject()
    {
        ClaimsPrincipal principal = new(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "legacy"), new Claim("sub", "first"), new Claim("sub", "second")]));
        Assert.Equal("first", principal.GetStaffSubject());
    }

    [Fact]
    public void Should_ReturnRemovedRolesAndPreserveUnrelatedClaims_When_ReplacingRoles()
    {
        ClaimsPrincipal principal = new(new ClaimsIdentity([
            new Claim("role", BusinessRoles.HeadOfficeUser), new Claim("role", "SysAdmin"), new Claim("sub", "staff")]));
        StaffRoleReplacement change = StaffRoleClaims.ReplaceBusinessRoles(principal, [BusinessRoles.SalesManager]);
        Assert.True(change.Changed);
        Assert.Equal([BusinessRoles.HeadOfficeUser], change.RemovedRoles);
        Assert.True(principal.HasClaim("role", "SysAdmin"));
        Assert.True(principal.HasClaim("role", BusinessRoles.SalesManager));
        Assert.False(principal.HasClaim("role", BusinessRoles.HeadOfficeUser));
        Assert.False(StaffRoleClaims.ReplaceBusinessRoles(principal, [BusinessRoles.SalesManager]).Changed);
    }
}
