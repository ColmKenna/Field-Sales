using FieldSales.Identity.Data;
using FieldSales.StaffAccess;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.DemoData;

public static class DemoStaffSeeder
{
    public const string HeadOfficeSubject = "fieldsales-demo-head-office";
    public const string ManagerSubject = "fieldsales-demo-manager";
    public const string AoifeSubject = "fieldsales-demo-aoife";
    public const string ColmSubject = "fieldsales-demo-colm";

    public static readonly IReadOnlyList<StaffDirectoryEntry> Staff =
    [
        new(HeadOfficeSubject, "Demo Head Office", [BusinessRoles.HeadOfficeUser], true),
        new(ManagerSubject, "Demo Sales Manager", [BusinessRoles.SalesManager], true),
        new(AoifeSubject, "Aoife Demo", [BusinessRoles.FieldSalesperson], true),
        new(ColmSubject, "Colm Demo", [BusinessRoles.FieldSalesperson], true)
    ];

    public static string Email(string subject) => subject switch
    {
        HeadOfficeSubject => "headoffice@demo.sales.local",
        ManagerSubject => "manager@demo.sales.local",
        AoifeSubject => "aoife@demo.sales.local",
        ColmSubject => "colm@demo.sales.local",
        _ => throw new ArgumentException("Unknown demo staff account.", nameof(subject))
    };

    public static Task<bool> SeedAsync(ApplicationDbContext db, UserManager<ApplicationUser> users,
        RoleManager<IdentityRole> roles, string password) => DemoSeedBatch.RunAsync(db, async () =>
    {
        foreach (string role in BusinessRoles.All)
            Ensure(await AdminAccountCreation.EnsureRoleAsync(roles, role));
        foreach (var staff in Staff)
        {
            // The receipt protects existing accounts on subsequent runs. If a reserved
            // identity already exists before the first seed, fail rather than adopt it.
            if (await users.FindByIdAsync(staff.Subject) is not null
                || await users.FindByNameAsync(Email(staff.Subject)) is not null)
                throw new InvalidOperationException("A reserved demo account already exists. Reset the demo environment before seeding.");
            var user = new ApplicationUser
            {
                Id = staff.Subject, UserName = Email(staff.Subject), Email = Email(staff.Subject),
                EmailConfirmed = true, FullName = staff.DisplayName
            };
            Ensure(await users.CreateAsync(user, password));
            Ensure(await users.AddToRolesAsync(user, staff.Roles));
        }
    });

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
    }
}
