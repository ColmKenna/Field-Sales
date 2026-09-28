using System.Security.Claims;
using FieldSales.StaffAccess;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddHttpClient<IStaffRoleLookup, HttpStaffRoleLookup>();

const string roleLookupUnavailableKey = "staff-role-lookup-unavailable";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Authentication:Authority"]
            ?? throw new InvalidOperationException("Authentication:Authority is required.");
        options.Audience = "fieldsales-api";
        options.RequireHttpsMetadata = true;
        options.MapInboundClaims = false;
        options.TokenValidationParameters.RoleClaimType = "role";
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                string? subject = context.Principal?.FindFirstValue("sub");
                string authorization = context.Request.Headers.Authorization.ToString();
                if (string.IsNullOrWhiteSpace(subject)
                    || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    context.Fail("The staff token has no subject or bearer value.");
                    return;
                }
                // The policy returns Forbidden for a valid token lacking this scope.
                // Such a token cannot call the identity lookup endpoint either.
                if (!context.Principal!.FindAll("scope")
                        .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                        .Contains("fieldsales.api"))
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
    options.AddPolicy("StaffApi", policy => policy
        .RequireAuthenticatedUser()
        .RequireRole(BusinessRoles.All)
        .RequireAssertion(context => context.User.FindAll("scope")
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains("fieldsales.api")));
    options.FallbackPolicy = options.GetPolicy("StaffApi");
});

WebApplication app = builder.Build();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/staff/session", (ClaimsPrincipal user) => Results.Ok(new
    {
        Subject = user.FindFirstValue("sub"),
        Roles = user.FindAll("role").Select(claim => claim.Value)
            .Where(BusinessRoles.Contains)
            .ToArray()
    }))
    .RequireAuthorization("StaffApi");
app.MapDefaultEndpoints();
app.Run();

public partial class Program;
