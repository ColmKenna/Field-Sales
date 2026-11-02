using FieldSales.AppHost;
using Microsoft.Extensions.Configuration;

namespace FieldSales.Api.UnitTests;

public sealed class TabletLanProfileTests
{
    [Fact]
    public void DisabledProfileNeedsNoLanSettings() =>
        Assert.Null(TabletLanProfile.Read(Config(), development: false));

    [Fact]
    public void EnabledProfileIsRefusedOutsideDevelopment() =>
        Assert.Throws<InvalidOperationException>(() => TabletLanProfile.Read(Config(("TabletLan:Enabled", "true")), false));

    [Theory]
    [InlineData(null)]
    [InlineData("localhost")]
    [InlineData("127.0.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("255.255.255.255")]
    [InlineData("224.0.0.1")]
    [InlineData("https://192.168.1.10:7201/")]
    [InlineData("::1")]
    public void EnabledProfileRejectsMissingOrInvalidLanHost(string? host) =>
        Assert.Throws<InvalidOperationException>(() => TabletLanProfile.Read(
            Config(("TabletLan:Enabled", "true"), ("TabletLan:Host", host)), true));

    [Fact]
    public void ProfileRequiresExistingCertificateAndPasswordThenUsesMatchingAuthority()
    {
        string certificate = Path.GetTempFileName();
        try
        {
            var settings = new List<(string, string?)>
            {
                ("TabletLan:Enabled", "true"), ("TabletLan:Host", "192.168.1.10")
            };
            Assert.Throws<InvalidOperationException>(() => TabletLanProfile.Read(Config(settings.ToArray()), true));
            settings.Add(("TabletLan:CertificatePath", certificate));
            Assert.Throws<InvalidOperationException>(() => TabletLanProfile.Read(Config(settings.ToArray()), true));
            settings.Add(("Parameters:tablet-lan-certificate-password", "test-only"));
            var profile = TabletLanProfile.Read(Config(settings.ToArray()), true)!;
            Assert.Equal("https://192.168.1.10:7201", profile.IdentityUrl);
            Assert.Equal("https://192.168.1.10:7204", profile.ApiUrl);
        }
        finally { File.Delete(certificate); }
    }

    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.Select(x => new KeyValuePair<string, string?>(x.Key, x.Value))).Build();
}
