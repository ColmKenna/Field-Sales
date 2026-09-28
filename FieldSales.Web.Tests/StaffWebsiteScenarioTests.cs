using System.Net;
using System.Text.RegularExpressions;
using FieldSales.Web.Data;
using FieldSales.Web.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FieldSales.Web.Tests;

public sealed class StaffWebsiteScenarioTests
{
    [Fact]
    public async Task Should_OpenHeadOfficeArea_When_HeadOfficeUserSignsIn()
    {
        await using StaffWebsiteFactory website = new();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser, "niamh", StaffRoles.HeadOfficeUser);

        await AssertLandingAsync(browser, "/HeadOffice", "head-office catalogue workspace is ready");
    }

    [Fact]
    public async Task Should_OpenManagerArea_When_SalesManagerSignsIn()
    {
        await using StaffWebsiteFactory website = new();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser, "aoife", StaffRoles.SalesManager);

        await AssertLandingAsync(browser, "/Manager", "manager workspace is ready");
    }

    [Fact]
    public async Task Should_OpenRepArea_When_FieldSalespersonSignsIn()
    {
        await using StaffWebsiteFactory website = new();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser, "colm", StaffRoles.FieldSalesperson);

        await AssertLandingAsync(browser, "/Rep", "field sales workspace is ready");
    }

    [Fact]
    public async Task Should_OpenLastPermittedArea_When_StaffHoldsSeveralRoles()
    {
        await using StaffWebsiteFactory website = new();
        using HttpClient browser = website.CreateBrowser();
        await SaveLastAreaAsync(website, "aoife", StaffAreas.Manager);
        await SignInAsync(browser, "aoife", StaffRoles.SalesManager, StaffRoles.HeadOfficeUser);

        Assert.Equal("/Manager", await HomeDestinationAsync(browser));
        string manager = await OpenAreaAsync(browser, "/Manager");
        Assert.Contains("Head office", manager);
        Assert.Contains("/HeadOffice", manager);

        string headOffice = await OpenAreaAsync(browser, "/HeadOffice");
        Assert.Contains("head-office catalogue workspace is ready", headOffice);
        Assert.Equal("/HeadOffice", await HomeDestinationAsync(browser));

        await using AsyncServiceScope scope = website.Services.CreateAsyncScope();
        StaffWebDbContext db = scope.ServiceProvider.GetRequiredService<StaffWebDbContext>();
        Assert.Equal(StaffAreas.HeadOffice, (await db.StaffAreaPreferences.SingleAsync()).Area);
        Assert.Single(await db.Tickets.ToListAsync());
    }

    [Fact]
    public async Task Should_OfferAreaChoice_When_MultiRoleStaffHasNoLastUsedArea()
    {
        await using StaffWebsiteFactory website = new();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser, "aoife", StaffRoles.SalesManager, StaffRoles.HeadOfficeUser);

        Assert.Equal("/Staff", await HomeDestinationAsync(browser));
        using HttpResponseMessage choice = await browser.GetAsync("/Staff");
        string html = await choice.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, choice.StatusCode);
        Assert.Contains("Choose your staff area", html);
        Assert.Contains("/Manager", html);
        Assert.Contains("/HeadOffice", html);
        Assert.DoesNotContain("/Rep", html);

        Assert.Contains("head-office catalogue workspace is ready", await OpenAreaAsync(browser, "/HeadOffice"));
        Assert.Equal("/HeadOffice", await HomeDestinationAsync(browser));
    }

    [Fact]
    public async Task Should_SkipRemovedLastUsedArea_When_StaffSignsIn()
    {
        await using StaffWebsiteFactory website = new();
        using HttpClient browser = website.CreateBrowser();
        await SaveLastAreaAsync(website, "aoife", StaffAreas.HeadOffice);
        await SignInAsync(browser, "aoife", StaffRoles.SalesManager);

        Assert.Equal("/Manager", await HomeDestinationAsync(browser));
        string manager = await OpenAreaAsync(browser, "/Manager");
        Assert.DoesNotContain("/HeadOffice", manager);

        using HttpResponseMessage unavailable = await browser.GetAsync("/HeadOffice");
        Assert.Equal(HttpStatusCode.Redirect, unavailable.StatusCode);
        Assert.Equal("/AccessDenied", unavailable.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task RejectedSignInShowsRetryAndKeepsStaffAreasProtected()
    {
        await using StaffWebsiteFactory website = new();
        using HttpClient browser = website.CreateBrowser();

        using HttpResponseMessage entry = await browser.GetAsync("/?error=sign-in");
        string html = await entry.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, entry.StatusCode);
        Assert.Contains("Couldn't sign in", html);
        Assert.Contains("Try again", html);
        Assert.DoesNotContain("catalogue workspace is ready", html);

        using HttpResponseMessage protectedPage = await browser.GetAsync("/HeadOffice");
        Assert.Equal(HttpStatusCode.Redirect, protectedPage.StatusCode);
        Assert.StartsWith("https://localhost:7201/connect/authorize",
            protectedPage.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Should_EndStaffAccess_When_TheUserSignsOut()
    {
        await using StaffWebsiteFactory website = new();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser, "niamh", StaffRoles.HeadOfficeUser);
        string page = await OpenAreaAsync(browser, "/HeadOffice");
        Match token = Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(token.Success, "The sign-out form must contain an antiforgery token.");

        using HttpResponseMessage signedOut = await browser.PostAsync("/SignOut",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token.Groups[1].Value
            }));
        Assert.Equal(HttpStatusCode.Redirect, signedOut.StatusCode);

        using HttpResponseMessage entry = await browser.GetAsync("/?signedOut=true");
        Assert.Contains("Signed out.", await entry.Content.ReadAsStringAsync());
        using HttpResponseMessage protectedPage = await browser.GetAsync("/HeadOffice");
        Assert.Equal(HttpStatusCode.Redirect, protectedPage.StatusCode);
        Assert.StartsWith("https://localhost:7201/connect/authorize",
            protectedPage.Headers.Location?.OriginalString);

        await using AsyncServiceScope scope = website.Services.CreateAsyncScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<StaffWebDbContext>()
            .Tickets.ToListAsync());
    }

    [Fact]
    public async Task Should_OpenRepArea_When_RepFollowsPermittedLink()
    {
        await using StaffWebsiteFactory website = new();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser, "colm", StaffRoles.FieldSalesperson);

        Assert.Contains("field sales workspace is ready", await OpenAreaAsync(browser, "/Rep"));
    }

    [Fact]
    public async Task Should_SaveAuthorizedForm_When_HeadOfficeRoleIsHeld()
    {
        await using StaffWebsiteFactory website = new();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser, "niamh", StaffRoles.HeadOfficeUser);

        using HttpResponseMessage save = await browser.PostAsync("/__test/head-office-save", new StringContent(""));
        Assert.Equal(HttpStatusCode.NoContent, save.StatusCode);

        await using AsyncServiceScope scope = website.Services.CreateAsyncScope();
        StaffWebDbContext db = scope.ServiceProvider.GetRequiredService<StaffWebDbContext>();
        StaffAreaPreference preference = await db.StaffAreaPreferences.SingleAsync();
        Assert.Equal("niamh", preference.SubjectId);
        Assert.Equal(StaffAreas.HeadOffice, preference.Area);
    }

    [Fact]
    public async Task Should_DenyUnheldArea_When_AStaffMemberUsesADirectLink()
    {
        await using StaffWebsiteFactory website = new();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser, "colm", StaffRoles.FieldSalesperson);

        using HttpResponseMessage read = await browser.GetAsync("/HeadOffice");
        Assert.Equal(HttpStatusCode.Redirect, read.StatusCode);
        Assert.Equal("/AccessDenied", read.Headers.Location?.AbsolutePath);
        Assert.DoesNotContain("head-office catalogue workspace", await read.Content.ReadAsStringAsync());

        using HttpResponseMessage action = await browser.PostAsync("/__test/head-office-save", new StringContent(""));
        Assert.Equal(HttpStatusCode.Redirect, action.StatusCode);
        Assert.Equal("/AccessDenied", action.Headers.Location?.AbsolutePath);

        await using (AsyncServiceScope scope = website.Services.CreateAsyncScope())
        {
            StaffWebDbContext db = scope.ServiceProvider.GetRequiredService<StaffWebDbContext>();
            Assert.Empty(await db.StaffAreaPreferences.ToListAsync());
        }

        using HttpResponseMessage denied = await browser.GetAsync("/AccessDenied");
        string html = await denied.Content.ReadAsStringAsync();
        Assert.Contains("That area is unavailable to your account", html);
        Assert.DoesNotContain("head-office catalogue workspace is ready", html);
        Assert.DoesNotContain("/HeadOffice", html);
    }

    private static async Task SignInAsync(HttpClient browser, string subject, params string[] roles)
    {
        string url = $"/__test/sign-in?subject={Uri.EscapeDataString(subject)}&roles={Uri.EscapeDataString(string.Join(',', roles))}";
        using HttpResponseMessage response = await browser.GetAsync(url);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static async Task SaveLastAreaAsync(StaffWebsiteFactory website, string subject, string area)
    {
        await using AsyncServiceScope scope = website.Services.CreateAsyncScope();
        StaffWebDbContext db = scope.ServiceProvider.GetRequiredService<StaffWebDbContext>();
        db.StaffAreaPreferences.Add(new StaffAreaPreference
        {
            SubjectId = subject,
            Area = area,
            UpdatedUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static async Task<string?> HomeDestinationAsync(HttpClient browser)
    {
        using HttpResponseMessage home = await browser.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, home.StatusCode);
        return home.Headers.Location?.OriginalString;
    }

    private static async Task AssertLandingAsync(HttpClient browser, string path, string content)
    {
        Assert.Equal(path, await HomeDestinationAsync(browser));
        Assert.Contains(content, await OpenAreaAsync(browser, path));
    }

    private static async Task<string> OpenAreaAsync(HttpClient browser, string path)
    {
        using HttpResponseMessage page = await browser.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        return await page.Content.ReadAsStringAsync();
    }
}
