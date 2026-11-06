using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FieldSales.Web.Security;

namespace FieldSales.Web.Tests;

/// <summary>WI-060 agreed scenarios 10 and 12: H-20's tier list and detail against scripted API replies. Validation and storage
/// are covered by PriceTierApiTests; the forms end to end by PriceTierIntegrationTests.</summary>
[Trait("Category", "Integration")]
public sealed class PriceTierPageTests
{
    private static readonly Guid TierB = Guid.NewGuid();
    private static readonly Guid Spf30 = Guid.NewGuid();
    private static readonly Guid HandCream = Guid.NewGuid();
    private static readonly Guid Sudocrem = Guid.NewGuid();
    private static string DetailPath => $"/HeadOffice/Tiers/{TierB}";

    [Fact]
    public async Task Should_ListTiersAndLeadTheDetailWithOverrides_When_HeadOfficeOpensPriceTiers()
    {
        using TierReplyHandler api = new();
        await using StaffWebsiteFactory website = new(api);
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);

        string index = await PageAsync(browser, "/HeadOffice/Index");
        string list = await PageAsync(browser, "/HeadOffice/Tiers");
        string detail = await PageAsync(browser, DetailPath);

        Assert.Contains("href=\"/HeadOffice/Tiers\"", index);
        Assert.Contains("2 tiers", list);
        Assert.Matches(@"Suncare Deal</a> · 12\.5% off base · 0 overrides", list);
        Assert.Matches(@"Tier B</a> · 8% off base · 2 overrides", list);
        Assert.True(list.IndexOf("Suncare Deal", StringComparison.Ordinal) < list.IndexOf("Tier B", StringComparison.Ordinal));
        Assert.Contains("<h1>Tier B</h1>", detail);
        Assert.Contains("8% off base", detail);
        Assert.Contains("<strong>Hand Cream 75ml</strong> · 15% off · €3.40 today (list €4.00)", detail);
        Assert.Contains("<strong>Loose Herbal Tea</strong> · fixed €4.00 per kg · list €4.80 per kg", detail);
        // H20.1: the overrides come before the tier's own terms.
        Assert.True(detail.IndexOf("id=\"overrides-heading\"", StringComparison.Ordinal) < detail.IndexOf("id=\"terms-heading\"", StringComparison.Ordinal));
        Assert.Equal(2, Regex.Matches(detail, "Remove override").Count);
        Assert.Contains("value=\"Tier B\"", detail);
    }

    [Fact]
    public async Task Should_MarkProductsThatAlreadyHaveAnOverride_When_SearchingToAddOne()
    {
        using TierReplyHandler api = new();
        await using StaffWebsiteFactory website = new(api);
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);

        string found = await PageAsync(browser, DetailPath + "?Find=cream");

        Assert.Equal("cream", api.SearchedFor);
        Assert.Contains("2 products match", found);
        string handCream = Item(found, "HAND-75 · Hand Cream 75ml");
        string sudocrem = Item(found, "SUDO-125 · Sudocrem 125g");
        Assert.Contains("list €4.00", handCream);
        Assert.Contains("Already has an override on this tier.", handCream);
        Assert.DoesNotContain("Add override", handCream);
        Assert.Contains("list €4.85", sudocrem);
        Assert.Contains("Add override", sudocrem);
        Assert.Contains($"value=\"{Sudocrem}\"", sudocrem);

        using HttpResponseMessage added = await PostOverrideAsync(browser, found, Sudocrem, "fixed", "4.25");
        Assert.Equal(HttpStatusCode.Redirect, added.StatusCode);
        Assert.Equal(DetailPath, added.Headers.Location!.OriginalString);
        Assert.Equal((Sudocrem, (SetPriceTierOverrideRequest?)new SetPriceTierOverrideRequest(null, 4.25m)), api.Set!.Value);
    }

    // The <li> of the search match that starts with this title.
    private static string Item(string html, string title)
    {
        int start = html.LastIndexOf("<li data-match>", html.IndexOf(title, StringComparison.Ordinal), StringComparison.Ordinal);
        return html[start..html.IndexOf("</li>", start, StringComparison.Ordinal)];
    }

    private static async Task SignInAsync(HttpClient browser)
    {
        using HttpResponseMessage response = await browser.GetAsync(
            $"/__test/sign-in?subject=niamh&roles={Uri.EscapeDataString(StaffRoles.HeadOfficeUser)}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static async Task<string> PageAsync(HttpClient browser, string path)
    {
        using HttpResponseMessage response = await browser.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }

    private static Task<HttpResponseMessage> PostOverrideAsync(HttpClient browser, string html, Guid product, string kind, string value)
    {
        Match token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(token.Success);
        return browser.PostAsync(DetailPath + "?handler=Override", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Override.ProductId"] = product.ToString(), ["Override.Kind"] = kind, ["Override.Value"] = value, ["Find"] = "cream",
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups[1].Value)
        }));
    }

    private sealed class TierReplyHandler : HttpMessageHandler
    {
        public string? SearchedFor { get; private set; }
        public (Guid Product, SetPriceTierOverrideRequest? Request)? Set { get; private set; }

        private static readonly PriceTierDetails Details = new(TierB, "Tier B", 8m,
        [
            new(HandCream, "HAND-75", "Hand Cream 75ml", "Each", 15m, null, 4.00m, 3.40m, new DateOnly(2026, 10, 1)),
            new(Guid.NewGuid(), "TEA-LOOSE", "Loose Herbal Tea", "kg", null, 4.00m, 4.80m, 4.00m, new DateOnly(2026, 10, 1))
        ]);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("test-access-token", request.Headers.Authorization!.Parameter);
            string path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get && path == "/pricing/tiers")
                return Json(new[] { new PriceTierListItem(Guid.NewGuid(), "Suncare Deal", 12.5m, 0), new PriceTierListItem(TierB, "Tier B", 8m, 2) });
            if (request.Method == HttpMethod.Get && path == $"/pricing/tiers/{TierB}") return Json(Details);
            if (request.Method == HttpMethod.Get && path == "/catalogue/products/search")
            {
                SearchedFor = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query)["q"];
                ProductSearchItem Item(Guid id, string code, string name, decimal price) =>
                    new(id, code, name, [], null, "Active", "Each", new(new DateOnly(2026, 9, 1), price));
                return Json(new ProductSearchResponse([Item(HandCream, "HAND-75", "Hand Cream 75ml", 4.00m),
                    Item(Sudocrem, "SUDO-125", "Sudocrem 125g", 4.85m)], [], []));
            }
            if (request.Method == HttpMethod.Put && path.StartsWith($"/pricing/tiers/{TierB}/overrides/", StringComparison.Ordinal))
            {
                Set = (Guid.Parse(path[(path.LastIndexOf('/') + 1)..]),
                    await request.Content!.ReadFromJsonAsync<SetPriceTierOverrideRequest>(cancellationToken));
                return new(HttpStatusCode.NoContent);
            }
            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        }

        private static HttpResponseMessage Json<T>(T body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };
    }
}
