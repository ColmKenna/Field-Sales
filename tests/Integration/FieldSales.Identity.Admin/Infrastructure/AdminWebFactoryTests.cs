using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FieldSales.Identity.Admin.Tests.Infrastructure;

[Trait("Category", "Integration")]
public sealed class AdminWebFactoryTests
{
    private static readonly string[] MonitoredEnvironmentVariables =
    [
        "Clients__StaffWebUri",
        "Clients__StaffWebSecret",
        "Seed__SysAdminPassword",
        "Seed__SysAdminEmail",
        "Seed__TestUserPassword",
        "ConnectionStrings__IdentityDb",
        "ConnectionStrings__IdentityConfigDb",
        "ConnectionStrings__IdentityOperationalDb"
    ];

    [Fact]
    public void Should_NotMutateProcessEnvironmentVariables_When_FactoryIsInstantiatedAndUsed()
    {
        var before = CaptureEnvironment();
        using (var factory = new AdminWebFactory())
        {
            AssertEnvironmentUnchanged(before);
        }
        AssertEnvironmentUnchanged(before);
    }

    [Fact]
    public async Task Should_ProvideRequiredConfigurationInMemory_When_HostIsStarted()
    {
        var before = CaptureEnvironment();
        using (var factory = new AdminWebFactory())
        {
            await factory.RunInScopeAsync(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();

                Assert.Equal("https://localhost:7203", config["Clients:StaffWebUri"]);
                Assert.Equal("secret", config["Clients:StaffWebSecret"]);
                Assert.Equal("Password123!", config["Seed:SysAdminPassword"]);
                Assert.Equal("admin@sales.local", config["Seed:SysAdminEmail"]);
                Assert.Equal("Password123!", config["Seed:TestUserPassword"]);
                Assert.Equal("Server=localhost;Database=dummy;", config["ConnectionStrings:IdentityDb"]);
                Assert.Equal("Server=localhost;Database=dummy;", config["ConnectionStrings:IdentityConfigDb"]);
                Assert.Equal("Server=localhost;Database=dummy;", config["ConnectionStrings:IdentityOperationalDb"]);

                return Task.CompletedTask;
            });

            AssertEnvironmentUnchanged(before);
        }
        AssertEnvironmentUnchanged(before);
    }

    [Fact]
    public async Task Should_SuccessfullyServeAnonymousLogin_When_ClientIsCreated()
    {
        using var factory = new AdminWebFactory();
        using var client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/Account/Login");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
    private static Dictionary<string, string?> CaptureEnvironment() =>
        MonitoredEnvironmentVariables.ToDictionary(name => name, Environment.GetEnvironmentVariable);

    private static void AssertEnvironmentUnchanged(IReadOnlyDictionary<string, string?> before)
    {
        foreach (var (name, value) in before)
            Assert.True(string.Equals(value, Environment.GetEnvironmentVariable(name), StringComparison.Ordinal),
                $"Factory lifecycle changed process setting {name}.");
    }
}
