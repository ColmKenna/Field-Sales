using FieldSales.Identity.Services.Validation;
using Xunit;

namespace FieldSales.Identity.Admin.Tests.Clients;

[Trait("Category", "Unit")]

public class UriValidationHelperTests
{
    [Theory]
    [InlineData("https://example.com/path")]
    [InlineData("https://example.com?query=value")]
    [InlineData("https://example.com#fragment")]
    [InlineData("https://user@example.com")]
    [InlineData("ftp://example.com")]
    public void TryNormalizeCorsOrigin_NonOriginValues_AreRejected(string value)
    {
        Assert.False(UriValidationHelper.TryNormalizeCorsOrigin(
            value, ValidationConstants.MaxClientCorsOriginLength, out _));
    }

    [Fact]
    public void TryNormalizeCorsOrigin_RootTrailingSlash_IsRemoved()
    {
        Assert.True(UriValidationHelper.TryNormalizeCorsOrigin(
            "https://Example.com:8443/", ValidationConstants.MaxClientCorsOriginLength, out string normalized));
        Assert.Equal("https://example.com:8443", normalized);
    }
}
