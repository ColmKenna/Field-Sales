using FieldSales.Identity.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FieldSales.Identity.Admin.Tests.Infrastructure;

public class AdminCreationPolicyTests
{
    private const string Email = "configured@example.test";
    private static Mock<UserManager<ApplicationUser>> Users() => new(Mock.Of<IUserStore<ApplicationUser>>(),
        null!, null!, null!, null!, null!, null!, null!, null!);
    private static Mock<RoleManager<IdentityRole>> Roles() => new(Mock.Of<IRoleStore<IdentityRole>>(), null!, null!, null!, null!);
    private static IConfiguration Configuration() => new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?> { ["AdminBootstrap:Email"] = Email, ["AdminBootstrap:Password"] = "Password123!" }).Build();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BootstrapUsesEmailAndSeedingUsesUsernameWithoutChangingExistingUsers(bool foundByEmail)
    {
        var users = Users();
        var roles = Roles();
        var existing = new ApplicationUser { UserName = "different-name", Email = Email };
        roles.Setup(r => r.RoleExistsAsync(Config.SysAdminRole)).ReturnsAsync(true);
        users.Setup(u => u.FindByEmailAsync(Email)).ReturnsAsync(foundByEmail ? existing : null);
        users.Setup(u => u.FindByNameAsync(Email)).ReturnsAsync(foundByEmail ? null : existing);
        users.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Existing user collision" }));
        using var services = new ServiceCollection().AddSingleton(users.Object).AddSingleton(roles.Object)
            .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance).BuildServiceProvider();
        await AdminBootstrapper.BootstrapSysAdminAsync(services, Configuration());
        users.Verify(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), foundByEmail ? Times.Never() : Times.Once());
        if (foundByEmail)
        {
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                SeedData.SeedSysAdminAsync(null!, users.Object, roles.Object, Email, "Password123!"));
            Assert.Contains("Existing user collision", failure.Message);
        }
        else await SeedData.SeedSysAdminAsync(null!, users.Object, roles.Object, Email, "Password123!");
        users.Verify(u => u.FindByEmailAsync(Email), Times.Once());
        users.Verify(u => u.FindByNameAsync(Email), Times.Once());
        users.Verify(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Once());
        users.Verify(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never());
        users.Verify(u => u.AddClaimsAsync(It.IsAny<ApplicationUser>(), It.IsAny<IEnumerable<System.Security.Claims.Claim>>()), Times.Never());
    }

    [Fact]
    public async Task RoleCreationFailureLogsDuringBootstrapAndThrowsDuringSeeding()
    {
        var users = Users();
        var roles = Roles();
        roles.Setup(r => r.RoleExistsAsync(Config.SysAdminRole)).ReturnsAsync(false);
        roles.Setup(r => r.CreateAsync(It.IsAny<IdentityRole>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Role failure" }));
        var logger = new Mock<ILogger>();
        var logging = new Mock<ILoggerFactory>();
        logging.Setup(l => l.CreateLogger(It.IsAny<string>())).Returns(logger.Object);
        using var services = new ServiceCollection().AddSingleton(users.Object).AddSingleton(roles.Object)
            .AddSingleton(logging.Object).BuildServiceProvider();
        await AdminBootstrapper.BootstrapSysAdminAsync(services, Configuration());
        Assert.Contains(logger.Invocations, call => call.Method.Name == "Log" && (LogLevel)call.Arguments[0] == LogLevel.Error);
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SeedData.SeedSysAdminAsync(null!, users.Object, roles.Object, Email, "Password123!"));
        Assert.Contains("Role failure", failure.Message);
        users.Verify(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never());
    }

    [Fact]
    public async Task SharedCreationPreservesUserFieldsAndFailureResult()
    {
        var users = Users();
        var failed = IdentityResult.Failed(new IdentityError { Description = "Creation failed" });
        users.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), "password")).ReturnsAsync(failed);
        var (user, result) = await AdminAccountCreation.CreateAsync(users.Object, Email, "Administrator", "password");
        Assert.Same(failed, result);
        Assert.Equal(Email, user.UserName);
        Assert.Equal(Email, user.Email);
        Assert.Equal("Administrator", user.FullName);
        Assert.True(user.EmailConfirmed);
    }
}
