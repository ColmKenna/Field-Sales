using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

namespace FieldSales.Identity.Admin.Tests.Infrastructure;

[Trait("Category", "Unit")]

public class StartupHelpersTests
{
    [Theory]
    [InlineData("Development", true, false)]
    [InlineData("Production", false, true)]
    [InlineData("Staging", false, true)]
    [InlineData("Testing", false, false)]
    public async Task Should_PreserveEnvironmentAndScopeLifetime_When_ExecutingSchemaWorkflow(string name, bool migrate, bool check)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(name);
        var context = new Context();
        using var services = new ServiceCollection().AddScoped(_ => context).BuildServiceProvider();
        await DatabaseStartup.EnsureSchemaAsync<Context>(services, environment.Object, "Database",
            db => { db.Migrated = true; return Task.CompletedTask; },
            db => { db.Checked = true; return Task.FromResult<IEnumerable<string>>([]); });
        Assert.Equal(migrate, context.Migrated);
        Assert.Equal(check, context.Checked);
        Assert.Equal(name != "Testing", context.Disposed);
    }

    [Fact]
    public async Task Should_FailWithExistingMessage_When_ProductionMigrationsArePending()
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(Environments.Production);
        using var services = new ServiceCollection().AddScoped<Context>().BuildServiceProvider();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => DatabaseStartup.EnsureSchemaAsync<Context>(
            services, environment.Object, "StaffWebDb", _ => throw new Exception("must not migrate"),
            _ => Task.FromResult<IEnumerable<string>>(["pending"])));
        Assert.Equal("StaffWebDb has pending migrations.", error.Message);
    }

    [Fact]
    public async Task Should_PreserveOriginalException_When_DatabaseFails()
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(Environments.Development);
        using var services = new ServiceCollection().AddScoped<Context>().BuildServiceProvider();
        var failure = new InvalidOperationException("migration failure");
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => DatabaseStartup.EnsureSchemaAsync<Context>(
            services, environment.Object, "Database", _ => throw failure, _ => throw new Exception("must not check")));
        Assert.Same(failure, actual);
    }

    [Fact]
    public async Task Should_NotResolveDatabase_When_EnvironmentIsTesting()
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns("Testing");
        using var services = new ServiceCollection().BuildServiceProvider();
        await DatabaseStartup.EnsureSchemaAsync<Context>(services, environment.Object, "Database",
            _ => throw new Exception("must not migrate"), _ => throw new Exception("must not check"));
    }

    [Fact]
    public void Should_KeepAbsolutePathsAndResolveRelativeAgainstContentRoot_When_ConfiguringCertificates()
    {
        string root = OperatingSystem.IsWindows() ? @"C:\app\content" : "/app/content";
        string absolute = OperatingSystem.IsWindows() ? @"C:\app\certs\certificate.pfx" : "/app/certs/certificate.pfx";
        Assert.Equal(absolute, StartupConfiguration.ResolveCertificatePath(root, absolute));
        Assert.Equal(Path.Combine(root, "keys", "certificate.pfx"),
            StartupConfiguration.ResolveCertificatePath(root, Path.Combine("keys", "certificate.pfx")));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Should_RetainBlankValuePolicy_When_LegacyNullOnlyConfigurationChecked(string value)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["key"] = value }).Build();
        Assert.Equal(value, configuration.Required("key", "required", allowBlank: true));
        Assert.Throws<InvalidOperationException>(() => configuration.Required("key"));
        Assert.Equal("required", Assert.Throws<InvalidOperationException>(() =>
            configuration.Required("missing", "required", allowBlank: true)).Message);
    }

    private sealed class Context : IDisposable
    {
        public bool Migrated { get; set; }
        public bool Checked { get; set; }
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }
}
