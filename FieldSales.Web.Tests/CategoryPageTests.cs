using System.Net;
using System.Text.RegularExpressions;
using FieldSales.Web.Security;

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
        string html, string name)
    {
        Match token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(token.Success, "The category form must include an antiforgery token.");
        return browser.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = name,
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups[1].Value)
        }));
    }
}
