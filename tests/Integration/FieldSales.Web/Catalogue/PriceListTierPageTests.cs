using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FieldSales.Web.Security;

namespace FieldSales.Web.Tests;

/// <summary>WI-061 agreed scenarios 8 and 11, and the pages for scenarios 4 and 10, against scripted API replies. Storage and the
/// API's rules are covered by PriceListTierApiTests; the forms end to end by PriceListTierIntegrationTests.</summary>
[Trait("Category", "Integration")]
public sealed class PriceListTierPageTests
{
    private static readonly Guid TierB = Guid.NewGuid();
    private static readonly Guid Deal = Guid.NewGuid();
    private static readonly Guid Spf30 = Guid.NewGuid();
    private static readonly Guid HandCream = Guid.NewGuid();
    private static readonly Guid Sudocrem = Guid.NewGuid();
    private static readonly DateOnly SetOn = new(2026, 10, 1);

    [Fact]
    public async Task Should_ShowEachTiersShape_When_HeadOfficeOpensTheTierList()
    {
        using TierReplyHandler api = new();
        await using StaffWebsiteFactory website = new(api);
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);

        string list = await PageAsync(browser, "/HeadOffice/Tiers");

        Assert.Contains("Suncare Deal 2027</a> · price list · 20 products", list);
        Assert.Contains("Tier B</a> · 8% off base · 2 overrides", list);
        Assert.Contains($"value=\"{PriceTierShapes.Percentage}\"", list);
        Assert.Contains($"value=\"{PriceTierShapes.PriceList}\"", list);

        // A price list ignores anything typed in the percentage box.
        using HttpResponseMessage created = await PostAsync(browser, "/HeadOffice/Tiers", list,
            new() { ["Name"] = "Hayfever Deal", ["Shape"] = PriceTierShapes.PriceList, ["PercentOff"] = "8" });
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        Assert.Equal(new SavePriceTierRequest("Hayfever Deal", null, PriceTierShapes.PriceList), api.Created);
    }

    [Fact]
    public async Task Should_SaveAndFlagTheRow_When_AFixedPriceOf13IsEnteredWhereBaseIs1250()
    {
        using TierReplyHandler api = new();
        await using StaffWebsiteFactory website = new(api);
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);

        string detail = await PageAsync(browser, $"/HeadOffice/Tiers/{Deal}?Find=cream");

        Assert.Contains("<h2 id=\"overrides-heading\">Listed prices</h2>", detail);
        Assert.Contains("price list · <a", detail);
        string spf30 = Item(detail, "data-override", "<strong>SPF30 Sun Lotion 200ml</strong>");
        string handCream = Item(detail, "data-override", "<strong>Hand Cream 75ml</strong>");
        Assert.Contains("fixed €13.00 · list €12.50", spf30);
        Assert.Contains("This is above list price and will never apply.", spf30);
        Assert.DoesNotContain("will never apply", handCream);
        Assert.Contains("Remove from list", handCream);
        Assert.DoesNotContain("Override.Kind\" aria-label", detail); // A price list takes a fixed price only: no percentage choice.
        Assert.Contains("Already listed on this tier.", Item(detail, "data-match", "HAND-75 · Hand Cream 75ml"));
        string sudocrem = Item(detail, "data-match", "SUDO-125 · Sudocrem 125g");
        Assert.Contains("Add to list", sudocrem);
        Assert.Contains($"name=\"Override.Kind\" value=\"{OverrideKinds.Fixed}\"", sudocrem);
        Assert.DoesNotContain("Products without an override", detail);

        // Saved, not refused: the warning is on the row, not in the way.
        using HttpResponseMessage saved = await PostAsync(browser, $"/HeadOffice/Tiers/{Deal}?handler=Override", detail,
            new() { ["Override.ProductId"] = Spf30.ToString(), ["Override.Kind"] = OverrideKinds.Fixed, ["Override.Value"] = "13.00" });
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.Equal((Deal, Spf30, (SetPriceTierOverrideRequest?)new SetPriceTierOverrideRequest(null, 13.00m)), api.Set!.Value);
    }

    [Fact]
    public async Task Should_WarnWhatStopsApplying_When_TierBIsChangingToAPriceList()
    {
        using TierReplyHandler api = new();
        await using StaffWebsiteFactory website = new(api);
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        string shapePath = $"/HeadOffice/Tiers/{TierB}/Shape";

        string detail = await PageAsync(browser, $"/HeadOffice/Tiers/{TierB}");
        string confirm = await PageAsync(browser, shapePath);

        Assert.Contains($"href=\"{shapePath}\">Change to a price list…</a>", detail);
        Assert.Contains("<h1>Change Tier B to a price list</h1>", confirm);
        Assert.Contains("Tier B's 8% off base will no longer apply. Products not listed will revert to base price.", confirm);
        Assert.Contains("This percentage override will be removed: Hand Cream 75ml.", confirm);
        Assert.Contains("Fixed prices stay as listed prices: SPF30 Sun Lotion 200ml.", confirm);
        Assert.Contains($"href=\"/HeadOffice/Tiers/{TierB}\">Cancel</a>", confirm);
        Assert.Null(api.ShapeChange); // Nothing changes until Confirm.

        using HttpResponseMessage confirmed = await PostAsync(browser, shapePath, confirm, []);
        Assert.Equal(HttpStatusCode.Redirect, confirmed.StatusCode);
        Assert.Equal($"/HeadOffice/Tiers/{TierB}", confirmed.Headers.Location!.OriginalString);
        Assert.Equal((TierB, (ChangePriceTierShapeRequest?)new ChangePriceTierShapeRequest(PriceTierShapes.PriceList, null)), api.ShapeChange!.Value);
    }

    [Fact]
    public async Task Should_KeepListedPricesAsFixedOverrides_When_APriceListChangesBackToPercentage()
    {
        using TierReplyHandler api = new();
        await using StaffWebsiteFactory website = new(api);
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        string shapePath = $"/HeadOffice/Tiers/{Deal}/Shape";

        string ask = await PageAsync(browser, shapePath);
        string outOfRange = await PageAsync(browser, shapePath + "?PercentOff=100");
        string review = await PageAsync(browser, shapePath + "?PercentOff=8");

        Assert.Contains("<h1>Change Suncare Deal 2027 to a percentage tier</h1>", ask);
        Assert.DoesNotContain("data-shape-warning", ask); // The percentage comes first, so the warning can name it.
        Assert.Contains("name=\"PercentOff\"", ask);
        Assert.DoesNotContain("data-shape-warning", outOfRange);
        Assert.Contains("Enter a percentage more than 0 and less than 100, with up to two decimal places.", outOfRange);
        Assert.Contains("Products not listed will get 8% off base.", review);
        Assert.Contains("Listed prices stay as fixed overrides: Hand Cream 75ml and SPF30 Sun Lotion 200ml.", review);
        Assert.Null(api.ShapeChange);

        using HttpResponseMessage refused = await PostAsync(browser, shapePath, review, new() { ["PercentOff"] = "8.125" });
        Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
        string refusedPage = WebUtility.HtmlDecode(await refused.Content.ReadAsStringAsync());
        Assert.Contains("Enter a percentage more than 0 and less than 100, with up to two decimal places.", refusedPage);
        Assert.Contains("value=\"8.125\"", refusedPage);

        using HttpResponseMessage confirmed = await PostAsync(browser, shapePath, review, new() { ["PercentOff"] = "8" });
        Assert.Equal(HttpStatusCode.Redirect, confirmed.StatusCode);
        Assert.Equal((Deal, (ChangePriceTierShapeRequest?)new ChangePriceTierShapeRequest(PriceTierShapes.Percentage, 8m)), api.ShapeChange!.Value);
    }

    private static class OverrideKinds
    {
        public const string Fixed = "fixed";
    }

    // The <li> carrying this attribute that contains this text.
    private static string Item(string html, string attribute, string text)
    {
        int start = html.LastIndexOf($"<li {attribute}>", html.IndexOf(text, StringComparison.Ordinal), StringComparison.Ordinal);
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

    private static Task<HttpResponseMessage> PostAsync(HttpClient browser, string path, string html, Dictionary<string, string> form)
    {
        Match token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(token.Success);
        form["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups[1].Value);
        return browser.PostAsync(path, new FormUrlEncodedContent(form));
    }

    private sealed class TierReplyHandler : HttpMessageHandler
    {
        public SavePriceTierRequest? Created { get; private set; }
        public (Guid Tier, Guid Product, SetPriceTierOverrideRequest? Request)? Set { get; private set; }
        public (Guid Tier, ChangePriceTierShapeRequest? Request)? ShapeChange { get; private set; }

        private static readonly PriceTierDetails TierBDetails = new(TierB, "Tier B", 8m,
        [
            new(HandCream, "HAND-75", "Hand Cream 75ml", "Each", 15m, null, 4.00m, 3.40m, SetOn, false),
            new(Spf30, "SUN-030", "SPF30 Sun Lotion 200ml", "Each", null, 10.50m, 12.50m, 10.50m, SetOn, false)
        ], PriceTierShapes.Percentage);

        private static readonly PriceTierDetails DealDetails = new(Deal, "Suncare Deal 2027", null,
        [
            new(HandCream, "HAND-75", "Hand Cream 75ml", "Each", null, 3.50m, 4.00m, 3.50m, SetOn, false),
            new(Spf30, "SUN-030", "SPF30 Sun Lotion 200ml", "Each", null, 13.00m, 12.50m, 13.00m, SetOn, true)
        ], PriceTierShapes.PriceList);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("test-access-token", request.Headers.Authorization!.Parameter);
            string path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get && path == "/pricing/tiers")
                return Json(new[]
                {
                    new PriceTierListItem(Deal, "Suncare Deal 2027", null, 20, PriceTierShapes.PriceList),
                    new PriceTierListItem(TierB, "Tier B", 8m, 2, PriceTierShapes.Percentage)
                });
            if (request.Method == HttpMethod.Post && path == "/pricing/tiers")
            {
                Created = await request.Content!.ReadFromJsonAsync<SavePriceTierRequest>(cancellationToken);
                return new(HttpStatusCode.Created)
                {
                    Content = JsonContent.Create(new PriceTierListItem(Guid.NewGuid(), Created!.Name!, null, 0, Created.Shape!))
                };
            }
            if (request.Method == HttpMethod.Get && path == $"/pricing/tiers/{TierB}") return Json(TierBDetails);
            if (request.Method == HttpMethod.Get && path == $"/pricing/tiers/{Deal}") return Json(DealDetails);
            if (request.Method == HttpMethod.Get && path == "/catalogue/products/search")
            {
                ProductSearchItem Item(Guid id, string code, string name, decimal price) =>
                    new(id, code, name, [], null, "Active", "Each", new(new DateOnly(2026, 9, 1), price));
                return Json(new ProductSearchResponse([Item(HandCream, "HAND-75", "Hand Cream 75ml", 4.00m),
                    Item(Sudocrem, "SUDO-125", "Sudocrem 125g", 4.85m)], [], []));
            }
            if (request.Method == HttpMethod.Put && path.EndsWith("/shape", StringComparison.Ordinal))
            {
                ChangePriceTierShapeRequest? change = await request.Content!.ReadFromJsonAsync<ChangePriceTierShapeRequest>(cancellationToken);
                // The API refuses a percentage beyond two decimal places (PriceListTierApiTests).
                if (change?.PercentOff == 8.125m)
                    return new(HttpStatusCode.BadRequest)
                    {
                        Content = JsonContent.Create(new ProductValidationErrors(new()
                        {
                            ["PercentOff"] = ["Enter a percentage more than 0 and less than 100, with up to two decimal places."]
                        }))
                    };
                ShapeChange = (Guid.Parse(path.Split('/')[3]), change);
                return new(HttpStatusCode.NoContent);
            }
            if (request.Method == HttpMethod.Put && path.Contains("/overrides/", StringComparison.Ordinal))
            {
                string[] parts = path.Split('/');
                Set = (Guid.Parse(parts[3]), Guid.Parse(parts[5]),
                    await request.Content!.ReadFromJsonAsync<SetPriceTierOverrideRequest>(cancellationToken));
                return new(HttpStatusCode.NoContent);
            }
            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        }

        private static HttpResponseMessage Json<T>(T body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };
    }
}
