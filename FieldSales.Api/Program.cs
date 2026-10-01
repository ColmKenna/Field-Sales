using System.Security.Claims;
using FieldSales.StaffAccess;
using FieldSales.Api.Catalogue;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddSqlServerDbContext<CatalogueDbContext>("CatalogueDb");
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IReferenceListStore, BrandListStore>();
builder.Services.AddScoped<IReferenceListStore, ProductProfileListStore>();
builder.Services.AddScoped<IReferenceListStore, AttributeNameListStore>();
builder.Services.AddScoped<IReferenceListStore, SupplierListStore>();
builder.Services.AddScoped<FieldSales.ReferenceData.IReferenceUsageSource, ProductBrandUsageSource>();
builder.Services.AddScoped<FieldSales.ReferenceData.IReferenceUsageSource, ProductReferenceUsageSource>();
builder.Services.AddScoped<FieldSales.ReferenceData.IReferenceUsageReader, ReferenceUsageReader>();
builder.Services.AddScoped<ProductBrandAssignments>();
builder.Services.AddScoped<ProductReferenceAssignments>();
builder.Services.AddHttpClient<IStaffRoleLookup, HttpStaffRoleLookup>().AddSafeReadResilience();

const string roleLookupUnavailableKey = StaffApiContract.LookupUnavailableKey;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Authentication:Authority"]
            ?? throw new InvalidOperationException("Authentication:Authority is required.");
        options.Audience = StaffApiContract.Audience;
        options.RequireHttpsMetadata = true;
        options.MapInboundClaims = false;
        options.TokenValidationParameters.RoleClaimType = "role";
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                string? subject = context.Principal.GetStaffSubject();
                string authorization = context.Request.Headers.Authorization.ToString();
                if (string.IsNullOrWhiteSpace(subject)
                    || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    context.Fail("The staff token has no subject or bearer value.");
                    return;
                }
                // The policy returns Forbidden for a valid token lacking this scope.
                // Such a token cannot call the identity lookup endpoint either.
                if (!context.Principal!.HasScope(StaffApiContract.Scope))
                    return;

                try
                {
                    IStaffRoleLookup lookup = context.HttpContext.RequestServices
                        .GetRequiredService<IStaffRoleLookup>();
                    StaffRoleLookupResult result = await lookup.GetRolesAsync(
                        authorization["Bearer ".Length..].Trim(), subject,
                        context.HttpContext.RequestAborted);
                    if (!result.TokenAccepted)
                    {
                        context.Fail("The staff token is no longer accepted.");
                        return;
                    }
                    StaffRoleClaims.ReplaceBusinessRoles(context.Principal!, result.Roles);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    context.HttpContext.Items[roleLookupUnavailableKey] = true;
                    context.Fail($"Current staff roles could not be checked: {exception.GetType().Name}");
                }
            },
            OnChallenge = context =>
            {
                if (context.HttpContext.Items.ContainsKey(roleLookupUnavailableKey))
                {
                    context.HandleResponse();
                    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                }
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("HeadOfficeCatalogue", policy => policy
        .RequireAuthenticatedUser()
        .RequireRole(BusinessRoles.HeadOfficeUser)
        .RequireAssertion(context => context.User.HasScope(StaffApiContract.Scope)));
    options.AddPolicy("StaffApi", policy => policy
        .RequireAuthenticatedUser()
        .RequireRole(BusinessRoles.All)
        .RequireAssertion(context => context.User.HasScope(StaffApiContract.Scope)));
    options.FallbackPolicy = options.GetPolicy("StaffApi");
});

WebApplication app = builder.Build();
if (app.Environment.IsDevelopment())
{
    await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<CatalogueDbContext>().Database.MigrateAsync();
}
else if (!app.Environment.IsEnvironment("Testing"))
{
    await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
    if ((await scope.ServiceProvider.GetRequiredService<CatalogueDbContext>()
            .Database.GetPendingMigrationsAsync()).Any())
        throw new InvalidOperationException("CatalogueDb has pending migrations.");
}
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/staff/session", (ClaimsPrincipal user) => Results.Ok(new StaffSessionResponse(
        user.GetStaffSubject()!, user.FindAll("role").Select(claim => claim.Value)
            .Where(BusinessRoles.Contains).ToArray())))
    .RequireAuthorization("StaffApi");
app.MapCatalogueEndpoints();
app.MapProductEndpoints();
app.MapReferenceListEndpoints();
app.MapDefaultEndpoints();
app.Run();

public partial class Program;
