using System.Net;
using AngleSharp;
using AngleSharp.Dom;
using FieldSales.Identity.Admin.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FieldSales.Identity.Admin.Tests.Shared;

/// <summary>
///     Each surface loads the shared stylesheet plus its own, and neither loads the other's.
///     shared.css must come first: the public and admin sheets build on its tokens and reset.
/// </summary>
public class StylesheetSeparationTests : IClassFixture<AdminWebFactory>
{
    private readonly AdminWebFactory _factory;

    public StylesheetSeparationTests(AdminWebFactory factory)
    {
        _factory = factory;
    }

    private async Task<string[]> StylesheetsAsync(string url, string? identity = null)
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        if (identity is not null) client.DefaultRequestHeaders.Add("X-Test-Auth", identity);

        HttpResponseMessage response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string content = await response.Content.ReadAsStringAsync();
        IBrowsingContext context = BrowsingContext.New(AngleSharp.Configuration.Default);
        IDocument document = await context.OpenAsync(request => request.Content(content));

        return document.QuerySelectorAll("link[rel=stylesheet]")
            .Select(link => link.GetAttribute("href")!.Split('?')[0])
            .ToArray();
    }

    [Theory]
    [InlineData("/Account/Login")]
    [InlineData("/Account/Logout")]
    [InlineData("/Account/AccessDenied")]
    [InlineData("/Error")]
    public async Task PublicPage_LoadsSharedThenPublic_AndNotAdmin(string url)
    {
        string[] sheets = await StylesheetsAsync(url, "anonymous");

        Assert.Equal(new[] { "/css/shared.css", "/css/public.css" }, sheets);
    }

    [Theory]
    [InlineData("/Admin")]
    [InlineData("/Admin/Clients")]
    [InlineData("/Admin/Users/Create")]
    public async Task AdminPage_LoadsSharedThenAdmin_AndNotPublic(string url)
    {
        string[] sheets = await StylesheetsAsync(url);

        Assert.Equal(new[] { "/css/shared.css", "/css/admin.css" }, sheets);
    }
}
