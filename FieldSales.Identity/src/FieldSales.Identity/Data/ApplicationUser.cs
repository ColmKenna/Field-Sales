using Microsoft.AspNetCore.Identity;

namespace FieldSales.Identity.Data;

public class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }
}