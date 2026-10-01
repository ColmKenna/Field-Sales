using FieldSales.Identity.Presentation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FieldSales.Identity.Data;

public static class AdminBootstrapper
{
    public static async Task BootstrapSysAdminAsync(IServiceProvider services, IConfiguration configuration)
    {
        using IServiceScope scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(AdminBootstrapper));

        string? email = configuration["AdminBootstrap:Email"] ?? configuration["Seed:SysAdminEmail"];
        string? password = configuration["AdminBootstrap:Password"] ?? configuration["Seed:SysAdminPassword"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogError("Admin bootstrap aborted: 'AdminBootstrap:Email' and 'AdminBootstrap:Password' must be configured.");
            return;
        }

        IdentityResult roleResult = await AdminAccountCreation.EnsureRoleAsync(roleManager, Config.SysAdminRole);
        if (!roleResult.Succeeded)
        {
            logger.LogError("Admin bootstrap failed to create role '{Role}': {Errors}",
                Config.SysAdminRole, roleResult.Describe(", "));
            return;
        }

        ApplicationUser? existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser != null)
        {
            logger.LogWarning("Admin bootstrap skipped: User '{Email}' already exists. Refusing to alter existing credentials or roles.", email);
            return;
        }

        var (user, result) = await AdminAccountCreation.CreateAsync(userManager, email, "System Administrator", password);
        if (!result.Succeeded)
        {
            logger.LogError("Admin bootstrap failed to create user: {Errors}", result.Describe(", "));
            return;
        }

        IdentityResult assignment = await userManager.AddToRoleAsync(user, Config.SysAdminRole);
        if (!assignment.Succeeded)
        {
            logger.LogError("Admin bootstrap failed to assign role '{Role}': {Errors}",
                Config.SysAdminRole, assignment.Describe(", "));
            return;
        }
        logger.LogInformation("Administrator account '{Email}' successfully created via one-time bootstrap.", email);
    }
}

