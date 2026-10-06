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
        // Ensure none of the target environment variables are set in the current process
        foreach (string variableName in MonitoredEnvironmentVariables)
        {
            Assert.Null(Environment.GetEnvironmentVariable(variableName));
        }

        using var factory = new AdminWebFactory();

        // Environment variables must still be null after factory instantiation and static type initialization
        foreach (string variableName in MonitoredEnvironmentVariables)
        {
            Assert.Null(Environment.GetEnvironmentVariable(variableName));
        }
    }

    [Fact]
    public async Task Should_ProvideRequiredConfigurationInMemory_When_HostIsStarted()
    {
        using var factory = new AdminWebFactory();

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

        // Ensure process environment remains unpolluted after running the host
        foreach (string variableName in MonitoredEnvironmentVariables)
        {
            Assert.Null(Environment.GetEnvironmentVariable(variableName));
        }
    }

    [Fact]
    public async Task Should_SuccessfullyServeAnonymousLogin_When_ClientIsCreated()
    {
        using var factory = new AdminWebFactory();
        using var client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/Account/Login");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
