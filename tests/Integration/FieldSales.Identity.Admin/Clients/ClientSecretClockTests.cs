using Duende.IdentityServer.Models;
using Duende.IdentityModel;
using Duende.IdentityServer.EntityFramework.DbContexts;
using FieldSales.Identity.Admin.Tests.Infrastructure;
using FieldSales.Identity.Services.Clients;
using FieldSales.Identity.Services.Secrets;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FieldSales.Identity.Admin.Tests.Clients;

public class ClientSecretClockTests
{
    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2040, 1, 2, 3, 4, 5, TimeSpan.Zero);
    }

    [Fact]
    public void Should_HashPlaintextAndPreserveExpiry_When_CreatedByFactory()
    {
        var clock = new Clock();
        var expiry = clock.GetUtcNow().UtcDateTime.AddDays(1);
        var generated = ClientSecretFactory.Create(clock, "description", expiry);
        Assert.NotEmpty(generated.Plaintext);
        Assert.Equal(generated.Plaintext.Sha256(), generated.Secret.Value);
        Assert.Equal(clock.GetUtcNow().UtcDateTime, generated.Secret.Created);
        Assert.Equal(expiry, generated.Secret.Expiration);
        Assert.Equal("SharedSecret", generated.Secret.Type);
    }

    [Fact]
    public async Task Should_UseInjectedClock_When_CreatingCloningAndRotatingSecrets()
    {
        await using var root = new AdminWebFactory();
        var clock = new Clock();
        await using var factory = root.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock))));
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var create = services.GetRequiredService<IClientCreateService>();
        var input = new ClientCreateInputModel
        {
            ClientId = "clock-source", ClientName = "Clock", SelectedPreset = ClientPresetIds.MachineToMachine,
            RequireClientSecret = true, RequirePkce = false, GrantTypes = [OidcConstants.GrantTypes.ClientCredentials]
        };
        var created = await create.CreateClientAsync(input);
        Assert.True(created.Success);
        input.ClientId = "clock-clone";
        var cloned = await create.CloneClientAsync("clock-source", input);
        Assert.True(cloned.Success);
        Assert.NotEqual(created.PlaintextSecret, cloned.PlaintextSecret);
        var details = services.GetRequiredService<ClientDetailsService>();
        var rejected = await details.GenerateClientSecretAsync(ClientId.Create("clock-source"), "expired",
            clock.GetUtcNow().UtcDateTime.AddSeconds(-1));
        Assert.False(rejected.Success);
        var generated = await details.GenerateClientSecretAsync(ClientId.Create("clock-source"), "rotated",
            clock.GetUtcNow().UtcDateTime.AddDays(1));
        Assert.True(generated.Success);
        var db = services.GetRequiredService<ConfigurationDbContext>();
        var stored = await db.Set<Duende.IdentityServer.EntityFramework.Entities.ClientSecret>().AsNoTracking().ToListAsync();
        Assert.Equal(3, stored.Count);
        Assert.All(stored, secret => Assert.Equal(clock.GetUtcNow().UtcDateTime, secret.Created));
        Assert.Contains(stored, secret => secret.Value == generated.PlaintextSecret!.Sha256()
            && secret.Expiration == clock.GetUtcNow().UtcDateTime.AddDays(1));
    }
}
