using FieldSales.Web.Data;
using FieldSales.Web.Catalogue;
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
    string certificatePath = builder.Configuration["DataProtection:CertificatePath"]
        ?? throw new InvalidOperationException("DataProtection:CertificatePath is required outside Development.");
    string certificatePassword = builder.Configuration["DataProtection:CertificatePassword"]
        ?? throw new InvalidOperationException("DataProtection:CertificatePassword is required outside Development.");
    string resolvedPath = Path.IsPathRooted(certificatePath)
        ? certificatePath
        : Path.Combine(builder.Environment.ContentRootPath, certificatePath);
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
    client.BaseAddress = new Uri(services.GetRequiredService<IConfiguration>()["StaffApi:BaseUrl"]
        ?? throw new InvalidOperationException("StaffApi:BaseUrl is required.")));
builder.Services.AddHttpClient<IStaffRoleLookup, HttpStaffRoleLookup>();
builder.Services.AddScoped<StaffCookieEvents>();
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
        options.Authority = builder.Configuration["Authentication:Authority"]
            ?? throw new InvalidOperationException("Authentication:Authority is required.");
        options.ClientId = "fieldsales-staff-web";
        options.ClientSecret = builder.Configuration["Authentication:ClientSecret"]
            ?? throw new InvalidOperationException("Authentication:ClientSecret is required.");
        options.ResponseType = "code";
        options.UsePkce = true;
        options.SaveTokens = true; // The ticket store protects these server-side.
        options.GetClaimsFromUserInfoEndpoint = true;
        options.MapInboundClaims = false;
        options.TokenValidationParameters.NameClaimType = "name";
        options.TokenValidationParameters.RoleClaimType = "role";
        options.ClaimActions.MapJsonKey("role", "role");
        options.Scope.Clear();
        foreach (string scope in new[] { "openid", "profile", "roles", "fieldsales.api", "offline_access" })
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
    options.AddPolicy("StaffMember", policy => policy.RequireRole(StaffRoles.All));
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Staff", "StaffMember");
    options.Conventions.AuthorizePage("/Rep/Index", StaffRoles.FieldSalesperson);
    options.Conventions.AuthorizePage("/Manager/Index", StaffRoles.SalesManager);
    options.Conventions.AuthorizeFolder("/HeadOffice", StaffRoles.HeadOfficeUser);
});

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<StaffWebDbContext>().Database.MigrateAsync();
}
else if (!app.Environment.IsEnvironment("Testing"))
{
    await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
    if ((await scope.ServiceProvider.GetRequiredService<StaffWebDbContext>()
            .Database.GetPendingMigrationsAsync()).Any())
        throw new InvalidOperationException("StaffWebDb has pending migrations.");
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
