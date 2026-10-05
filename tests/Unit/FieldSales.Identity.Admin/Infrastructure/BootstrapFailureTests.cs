using FieldSales.Identity.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace FieldSales.Identity.Admin.Tests.Infrastructure;

[Trait("Category", "Unit")]

public class BootstrapFailureTests
{
    [Fact]
    public async Task FailedRoleAssignment_DoesNotLogBootstrapSuccess()
    {
        var users = new Mock<UserManager<ApplicationUser>>(Mock.Of<IUserStore<ApplicationUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);
        var roles = new Mock<RoleManager<IdentityRole>>(Mock.Of<IRoleStore<IdentityRole>>(), null!, null!, null!, null!);
        var logger = new Mock<ILogger>();
        var logging = new Mock<ILoggerFactory>();
        logging.Setup(factory => factory.CreateLogger(It.IsAny<string>())).Returns(logger.Object);
        roles.Setup(manager => manager.RoleExistsAsync(Config.SysAdminRole)).ReturnsAsync(true);
        users.Setup(manager => manager.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);
        users.Setup(manager => manager.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);
        users.Setup(manager => manager.AddToRoleAsync(It.IsAny<ApplicationUser>(), Config.SysAdminRole))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Assignment failed" }));
        using var services = new ServiceCollection().AddSingleton(users.Object).AddSingleton(roles.Object)
            .AddSingleton(logging.Object).BuildServiceProvider();
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AdminBootstrap:Email"] = "bootstrap@example.test", ["AdminBootstrap:Password"] = "Password123!"
        }).Build();
        await AdminBootstrapper.BootstrapSysAdminAsync(services, configuration);
        Assert.Contains(logger.Invocations, invocation => invocation.Method.Name == "Log" && (LogLevel)invocation.Arguments[0] == LogLevel.Error);
        Assert.DoesNotContain(logger.Invocations, invocation => invocation.Method.Name == "Log" && (LogLevel)invocation.Arguments[0] == LogLevel.Information);
    }
}
