using FieldSales.Web.Security;

namespace FieldSales.Web.Tests;

[Trait("Category", "Unit")]
public sealed class StaffAreaServiceTests
{
    [Theory]
    [InlineData("/Rep", "rep")]
    [InlineData("/Manager/Reports?month=4", "manager")]
    [InlineData("/HeadOffice/#tools", "head-office")]
    [InlineData("/HeadOffice/Categories", "head-office")]
    [InlineData("//attacker.test/Rep", null)]
    [InlineData("https://attacker.test/Manager", null)]
    [InlineData("/Representative", null)]
    public void LocalPathMapsOnlyToItsStaffArea(string url, string? expected) =>
        Assert.Equal(expected, StaffAreas.ForLocalUrl(url)?.Key);

}
