using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Authentication:Authority"]
            ?? throw new InvalidOperationException("Authentication:Authority is required.");
        options.Audience = "fieldsales-api";
        options.RequireHttpsMetadata = true;
        options.MapInboundClaims = false;
        options.TokenValidationParameters.RoleClaimType = "role";
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("StaffApi", policy => policy
        .RequireAuthenticatedUser()
        .RequireRole("Field Salesperson", "Sales Manager", "Head Office User")
        .RequireAssertion(context => context.User.FindAll("scope")
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains("fieldsales.api")));
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});

WebApplication app = builder.Build();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/staff/session", (ClaimsPrincipal user) => Results.Ok(new
    {
        Subject = user.FindFirstValue("sub"),
        Roles = user.FindAll("role").Select(claim => claim.Value)
            .Where(role => role is "Field Salesperson" or "Sales Manager" or "Head Office User")
            .ToArray()
    }))
    .RequireAuthorization("StaffApi");
app.MapDefaultEndpoints();
app.Run();

public partial class Program;
