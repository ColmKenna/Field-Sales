using System.Security.Cryptography.X509Certificates;
using FieldSales.Identity.Configuration;

namespace FieldSales.Identity.Admin.Tests.Infrastructure;

/// <summary>
///     Unit tests for <see cref="Pkcs12CertificateLoader" /> storage flag selection.
///     Physical file loading tests are located in the integration test suite.
/// </summary>
[Trait("Category", "Unit")]
public sealed class Pkcs12CertificateLoaderTests
{
    [Fact]
    public void KeyStorageFlags_ReflectsRunningOperatingSystem()
    {
        X509KeyStorageFlags expected = OperatingSystem.IsMacOS()
            ? X509KeyStorageFlags.DefaultKeySet
            : X509KeyStorageFlags.EphemeralKeySet;

        Assert.Equal(expected, Pkcs12CertificateLoader.KeyStorageFlags);
    }
}
