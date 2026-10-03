using FieldSales.Web.Data;
using FieldSales.Web.Catalogue;
using FieldSales.Web.Directory;
using FieldSales.Web.Security;
using FieldSales.StaffAccess;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddSqlServerDbContext<StaffWebDbContext>("StaffWebDb");
builder.Services.AddSingleton(TimeProvider.System);

IDataProtectionBuilder dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("FieldSales.Web")
    .PersistKeysToDbContext<StaffWebDbContext>();

if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing"))
{
    string certificatePath = builder.Configuration.Required("DataProtection:CertificatePath", "DataProtection:CertificatePath is required outside Development.", allowBlank: true);
    string certificatePassword = builder.Configuration.Required("DataProtection:CertificatePassword", "DataProtection:CertificatePassword is required outside Development.", allowBlank: true);
    string resolvedPath = StartupConfiguration.ResolveCertificatePath(builder.Environment.ContentRootPath, certificatePath);
    dataProtection.ProtectKeysWithCertificate(
        X509CertificateLoader.LoadPkcs12FromFile(resolvedPath, certificatePassword));
}

builder.Services.AddSingleton<SqlTicketStore>();
builder.Services.AddScoped<StaffAreaService>();
builder.Services.AddSingleton<AccessChangedTokenService>();
builder.Services.AddSingleton<IPostConfigureOptions<CookieAuthenticationOptions>, TicketStoreCookieOptions>();
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<CatalogueApiClient>((services, client) =>
    client.BaseAddress = new Uri(services.GetRequiredService<IConfiguration>().Required("StaffApi:BaseUrl", "StaffApi:BaseUrl is required.", allowBlank: true))).AddSafeReadResilience();
builder.Services.AddHttpClient<IStaffRoleLookup, HttpStaffRoleLookup>().AddSafeReadResilience();
builder.Services.AddHttpClient<DirectoryApiClient>((services, client) =>
    client.BaseAddress = new Uri(services.GetRequiredService<IConfiguration>().Required("StaffApi:BaseUrl", "StaffApi:BaseUrl is required.", allowBlank: true))).AddSafeReadResilience();
builder.Services.AddScoped<StaffCookieEvents>();
builder.Services.AddHttpClient<FieldSales.Web.Coverage.CoverageApiClient>((services, client) =>
    client.BaseAddress = new Uri(services.GetRequiredService<IConfiguration>().Required("StaffApi:BaseUrl", "StaffApi:BaseUrl is required.", allowBlank: true))).AddSafeReadResilience();
builder.Services.AddHostedService<ExpiredTicketsCleanupService>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.Cookie.Name = "__Host-FieldSalesStaff";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.Path = "/";
        options.AccessDeniedPath = "/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.EventsType = typeof(StaffCookieEvents);
    })
    .AddOpenIdConnect(options =>
    {
        options.Authority = builder.Configuration.Required("Authentication:Authority", "Authentication:Authority is required.", allowBlank: true);
        options.ClientId = StaffApiContract.WebClientId;
        options.ClientSecret = builder.Configuration.Required("Authentication:ClientSecret", "Authentication:ClientSecret is required.", allowBlank: true);
        options.ResponseType = "code";
        options.UsePkce = true;
        options.SaveTokens = true; // The ticket store protects these server-side.
        options.GetClaimsFromUserInfoEndpoint = true;
        options.MapInboundClaims = false;
        options.TokenValidationParameters.NameClaimType = "name";
        options.TokenValidationParameters.RoleClaimType = "role";
        options.ClaimActions.MapJsonKey("role", "role");
        options.Scope.Clear();
        foreach (string scope in new[] { "openid", "profile", "roles", StaffApiContract.Scope, "offline_access" })
            options.Scope.Add(scope);
        options.Events.OnRemoteFailure = context =>
        {
            context.HandleResponse();
            context.Response.Redirect("/?error=sign-in");
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(StaffRoles.FieldSalesperson, policy => policy.RequireRole(StaffRoles.FieldSalesperson));
    options.AddPolicy(StaffRoles.SalesManager, policy => policy.RequireRole(StaffRoles.SalesManager));
    options.AddPolicy(StaffRoles.HeadOfficeUser, policy => policy.RequireRole(StaffRoles.HeadOfficeUser));
    options.AddPolicy("ManageCoverage", policy => policy.RequireRole(StaffRoles.SalesManager, StaffRoles.HeadOfficeUser));
    options.AddPolicy("StaffMember", policy => policy.RequireRole(StaffRoles.All));
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Staff", "StaffMember");
    options.Conventions.AuthorizePage("/Rep/Index", StaffRoles.FieldSalesperson);
    options.Conventions.AuthorizePage("/Manager/Index", StaffRoles.SalesManager);
    options.Conventions.AuthorizeFolder("/Coverage", "ManageCoverage");
    options.Conventions.AuthorizeFolder("/HeadOffice", StaffRoles.HeadOfficeUser);
});

WebApplication app = builder.Build();

await DatabaseStartup.EnsureSchemaAsync<StaffWebDbContext>(app.Services, app.Environment, "StaffWebDb",
    context => context.Database.MigrateAsync(), context => context.Database.GetPendingMigrationsAsync());
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.UseExceptionHandler(error => error.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsync("The staff workspace is unavailable.");
    }));
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.Use(async (context, next) =>
{
    if (context.Items.ContainsKey(StaffCookieEvents.LookupUnavailableKey))
    {
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await context.Response.WriteAsync("The staff workspace is temporarily unavailable.");
        return;
    }
    await next();
});
app.UseAuthorization();
app.MapRazorPages();
if (app.Environment.IsEnvironment("Testing"))
{
    // A representative write for role-boundary tests until WI-004 adds catalogue actions.
    // It persists a subject-keyed area preference, so denied requests leave visible evidence.
    app.MapPost("/HeadOffice/__test/save", async (
            ClaimsPrincipal user, StaffAreaService areas, HttpContext context) =>
            await areas.RememberAreaAsync(user, StaffAreas.HeadOffice, context.RequestAborted)
                ? Results.NoContent()
                : Results.Forbid())
        .RequireAuthorization(StaffRoles.HeadOfficeUser);
}
app.MapDefaultEndpoints();
app.Run();

public partial class Program;
