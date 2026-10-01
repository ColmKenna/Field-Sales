using System.Net;
using AngleSharp;
using AngleSharp.Html.Dom;
using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Entities;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FieldSales.Identity.Admin.Tests.Infrastructure;

public class ClaimEditorBindingTests
{
    [Theory]
    [InlineData("ApiScopes", "AddClaim", false)]
    [InlineData("ApiScopes", "RemoveClaim", false)]
    [InlineData("IdentityResources", "AddClaim", false)]
    [InlineData("IdentityResources", "RemoveClaim", false)]
    [InlineData("ApiScopes", "AddClaim", true)]
    [InlineData("ApiScopes", "RemoveClaim", true)]
    [InlineData("IdentityResources", "AddClaim", true)]
    [InlineData("IdentityResources", "RemoveClaim", true)]
    public async Task ClaimMutation_UsesOnlyUrlTarget(string area, string handler, bool missingUrlTarget)
    {
        using var factory = new AdminWebFactory();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        string target = "target-" + Guid.NewGuid().ToString("N");
        string other = "other-" + Guid.NewGuid().ToString("N");
        bool removing = handler == "RemoveClaim";
        await factory.RunInScopeAsync(async services =>
        {
            var db = services.GetRequiredService<ConfigurationDbContext>();
            foreach (string name in new[] { target, other })
                if (area == "ApiScopes")
                    db.ApiScopes.Add(new ApiScope { Name = name, UserClaims = removing ? [new ApiScopeClaim { Type = "review_claim" }] : [] });
                else
                    db.IdentityResources.Add(new IdentityResource { Name = name, UserClaims = removing ? [new IdentityResourceClaim { Type = "review_claim" }] : [] });
            await db.SaveChangesAsync();
        });
        string path = $"/Admin/{area}/Edit";
        using HttpResponseMessage form = await client.GetAsync(path + "?name=" + target);
        string html = await form.Content.ReadAsStringAsync();
        var document = await BrowsingContext.New(AngleSharp.Configuration.Default)
            .OpenAsync(request => request.Content(html));
        string token = Assert.IsAssignableFrom<IHtmlInputElement>(document.QuerySelector("input[name='__RequestVerificationToken']")).Value;
        using var request = new HttpRequestMessage(HttpMethod.Post,
            path + $"?handler={handler}&name=" + (missingUrlTarget ? "" : target));
        request.Headers.Add("Cookie", form.Headers.GetValues("Set-Cookie").First(cookie => cookie.StartsWith(".AspNetCore.Antiforgery")));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token, ["name"] = other, ["claimType"] = "review_claim"
        });
        using HttpResponseMessage response = await client.SendAsync(request);
        Assert.Equal(missingUrlTarget ? HttpStatusCode.NotFound : HttpStatusCode.Redirect, response.StatusCode);
        if (!missingUrlTarget) Assert.Contains("name=" + target, response.Headers.Location!.OriginalString);
        await factory.RunInScopeAsync(async services =>
        {
            var db = services.GetRequiredService<ConfigurationDbContext>();
            async Task<bool> HasClaim(string name) => area == "ApiScopes"
                ? await db.ApiScopes.AnyAsync(item => item.Name == name && item.UserClaims.Any(claim => claim.Type == "review_claim"))
                : await db.IdentityResources.AnyAsync(item => item.Name == name && item.UserClaims.Any(claim => claim.Type == "review_claim"));
            Assert.Equal(removing, await HasClaim(other));
            Assert.Equal(missingUrlTarget ? removing : !removing, await HasClaim(target));
            var auditDb = services.GetRequiredService<FieldSales.Identity.Data.ApplicationDbContext>();
            var events = await auditDb.AuditLogEntries.Where(entry => entry.TargetId == target || entry.TargetId == other).ToListAsync();
            if (missingUrlTarget) Assert.Empty(events);
            else Assert.Equal(target, Assert.Single(events).TargetId);
        });
    }
}
