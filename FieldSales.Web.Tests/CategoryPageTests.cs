using System.Net;
using System.Text.RegularExpressions;
using FieldSales.Web.Security;
using FieldSales.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FieldSales.Web.Tests;

public sealed class CategoryPageTests
{
    [Fact]
    public async Task HeadOfficeStaffCanCreateRootsAndChildrenThroughTheWebsite()
    {
        await using StaffWebsiteFactory website = new();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser, "niamh", StaffRoles.HeadOfficeUser);

        string rootsPage = await GetHtmlAsync(browser, "/HeadOffice/Categories");
        Assert.Contains("Add root category", rootsPage);
        using HttpResponseMessage root = await PostFormAsync(browser, "/HeadOffice/Categories", rootsPage, "Suncare");
        Assert.Equal(HttpStatusCode.Redirect, root.StatusCode);
        string detailUrl = root.Headers.Location!.OriginalString;
        Assert.Matches("^/HeadOffice/Categories/[0-9a-fA-F-]{36}$", detailUrl);
        string detail = await GetHtmlAsync(browser, detailUrl);
        Assert.Contains("Suncare", detail);
        Assert.Contains("Add subcategory", detail);

        using HttpResponseMessage child = await PostFormAsync(browser, detailUrl, detail, "Lotions");
        Assert.Equal(HttpStatusCode.Redirect, child.StatusCode);
        string leaf = await GetHtmlAsync(browser, child.Headers.Location!.OriginalString);
        Assert.Contains("Suncare", leaf);
        Assert.Contains("Lotions", leaf);
        Assert.Equal(2, website.Catalogue.Count);

        using HttpResponseMessage duplicate = await PostFormAsync(browser, detailUrl, detail, "LOTIONS");
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        Assert.Contains("A category with this name already exists here.",
            await duplicate.Content.ReadAsStringAsync());
        Assert.Equal(2, website.Catalogue.Count);
    }

    [Theory]
    [InlineData(StaffRoles.FieldSalesperson)]
    [InlineData(StaffRoles.SalesManager)]
    public async Task OtherStaffCannotReadOrPostCategories(string role)
    {
        await using StaffWebsiteFactory website = new();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser, "other", role);

        using HttpResponseMessage read = await browser.GetAsync("/HeadOffice/Categories");
        Assert.Equal("/AccessDenied", read.Headers.Location?.AbsolutePath);
        using HttpResponseMessage write = await browser.PostAsync("/HeadOffice/Categories",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["Name"] = "Denied" }));
        Assert.Equal("/AccessDenied", write.Headers.Location?.AbsolutePath);
        Assert.Equal(0, website.Catalogue.Count);
    }

    [Fact]
    public async Task RenamingAncestorRefreshesDescendantPathWithoutChangingItsUrl()
    {
        await using StaffWebsiteFactory website = new();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser, "niamh", StaffRoles.HeadOfficeUser);
        string roots = await GetHtmlAsync(browser, "/HeadOffice/Categories");
        using HttpResponseMessage root = await PostFormAsync(browser, "/HeadOffice/Categories", roots, "Health");
        string rootUrl = root.Headers.Location!.OriginalString;
        string rootPage = await GetHtmlAsync(browser, rootUrl);
        using HttpResponseMessage child = await PostFormAsync(browser, rootUrl, rootPage, "Suncare");
        string childUrl = child.Headers.Location!.OriginalString;
        string childPage = await GetHtmlAsync(browser, childUrl);
        using HttpResponseMessage leaf = await PostFormAsync(browser, childUrl, childPage, "Lotions");
        string leafUrl = leaf.Headers.Location!.OriginalString;
        using HttpResponseMessage sibling = await PostFormAsync(browser, rootUrl, rootPage, "Body Care");
        Assert.Equal(HttpStatusCode.Redirect, sibling.StatusCode);

        using HttpResponseMessage renamed = await PostFormAsync(browser,
            $"{childUrl}?handler=Rename", childPage, "Sun Care", "RenameName");
        Assert.Equal(HttpStatusCode.Redirect, renamed.StatusCode);
        Assert.Equal(childUrl, renamed.Headers.Location?.OriginalString);
        string descendant = await GetHtmlAsync(browser, leafUrl);
        Assert.Contains("Sun Care", descendant);
        Assert.DoesNotContain("Suncare", descendant);
        Assert.Contains("Lotions", descendant);
        Assert.Equal(4, website.Catalogue.Count);

        string renamedPage = await GetHtmlAsync(browser, childUrl);
        using HttpResponseMessage duplicate = await PostFormAsync(browser,
            $"{childUrl}?handler=Rename", renamedPage, "body care", "RenameName");
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        Assert.Contains("A category with this name already exists here.",
            await duplicate.Content.ReadAsStringAsync());
        Assert.Contains("Sun Care", await GetHtmlAsync(browser, childUrl));
        Assert.Equal(4, website.Catalogue.Count);
    }

    [Fact]
    public async Task RemovedRoleRejectsOpenRenameFormWithoutSavingOrReplayingIt()
    {
        await using StaffWebsiteFactory website = new();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser, "niamh", StaffRoles.HeadOfficeUser);
        string roots = await GetHtmlAsync(browser, "/HeadOffice/Categories");
        using HttpResponseMessage root = await PostFormAsync(browser, "/HeadOffice/Categories", roots, "Suncare");
        string categoryUrl = root.Headers.Location!.OriginalString;
        string openForm = await GetHtmlAsync(browser, categoryUrl);

        website.Roles.SetRoles("niamh");
        using HttpResponseMessage denied = await PostFormAsync(browser,
            $"{categoryUrl}?handler=Rename", openForm, "Changed", "RenameName");
        Assert.StartsWith("/AccessChanged?state=", denied.Headers.Location?.OriginalString);
        using HttpResponseMessage message = await browser.GetAsync(denied.Headers.Location);
        Assert.Contains("A change you just tried to make was not saved.",
            await message.Content.ReadAsStringAsync());
        website.Roles.SetRoles("niamh", StaffRoles.HeadOfficeUser);
        Assert.Contains("Suncare", await GetHtmlAsync(browser, categoryUrl));
        Assert.DoesNotContain("Changed", await GetHtmlAsync(browser, categoryUrl));
        Assert.Equal(1, website.Catalogue.Count);
    }

    [Fact]
    public async Task ExpiredSessionRejectsOpenRenameFormWithoutReplayingIt()
    {
        await using StaffWebsiteFactory website = new();
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser, "niamh", StaffRoles.HeadOfficeUser);
        string roots = await GetHtmlAsync(browser, "/HeadOffice/Categories");
        using HttpResponseMessage root = await PostFormAsync(browser, "/HeadOffice/Categories", roots, "Suncare");
        string categoryUrl = root.Headers.Location!.OriginalString;
        string openForm = await GetHtmlAsync(browser, categoryUrl);
        await using (AsyncServiceScope scope = website.Services.CreateAsyncScope())
        {
            StaffWebDbContext db = scope.ServiceProvider.GetRequiredService<StaffWebDbContext>();
            StoredTicket ticket = await db.Tickets.SingleAsync();
            ticket.ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        using HttpResponseMessage denied = await PostFormAsync(browser,
            $"{categoryUrl}?handler=Rename", openForm, "Changed", "RenameName");
        Assert.StartsWith("https://localhost:7201/connect/authorize", denied.Headers.Location?.OriginalString);
        await SignInAsync(browser, "niamh", StaffRoles.HeadOfficeUser);
        Assert.Contains("Suncare", await GetHtmlAsync(browser, categoryUrl));
        Assert.DoesNotContain("Changed", await GetHtmlAsync(browser, categoryUrl));
        Assert.Equal(1, website.Catalogue.Count);
    }

    private static async Task SignInAsync(HttpClient browser, string subject, string role)
    {
        using HttpResponseMessage response = await browser.GetAsync(
            $"/__test/sign-in?subject={subject}&roles={Uri.EscapeDataString(role)}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static async Task<string> GetHtmlAsync(HttpClient browser, string path)
    {
        using HttpResponseMessage response = await browser.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static Task<HttpResponseMessage> PostFormAsync(HttpClient browser, string path,
        string html, string name, string field = "Name")
    {
        Match token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(token.Success, "The category form must include an antiforgery token.");
        return browser.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [field] = name,
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups[1].Value)
        }));
    }
}
