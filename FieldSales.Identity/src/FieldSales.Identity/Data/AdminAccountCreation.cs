using Microsoft.AspNetCore.Identity;

namespace FieldSales.Identity.Data;

public static class AdminAccountCreation
{
    public static async Task<IdentityResult> EnsureRoleAsync(RoleManager<IdentityRole> roles, string role) =>
        await roles.RoleExistsAsync(role) ? IdentityResult.Success : await roles.CreateAsync(new IdentityRole(role));

    public static async Task<(ApplicationUser User, IdentityResult Result)> CreateAsync(
        UserManager<ApplicationUser> users, string email, string fullName, string password)
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = fullName
        };
        return (user, await users.CreateAsync(user, password));
    }
}
