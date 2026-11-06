using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FieldSales.Directory.Contracts;
using FieldSales.Web.Security;

namespace FieldSales.Web.Tests;

/// <summary>WI-063 agreed scenarios 2 and 3: the Pricing section of the customer record (H-21) against scripted API replies. The
/// rules, storage and the engine's prices are covered by CustomerTierPricingTests; the section end to end by
/// CustomerTierIntegrationTests.</summary>
[Trait("Category", "Integration")]
public sealed class CustomerTierPageTests
{
    private static readonly Guid Hickeys = Guid.NewGuid();
    private static readonly Guid TierB = Guid.NewGuid();
    private static readonly Guid Deal = Guid.NewGuid();
    private static readonly Guid TierC = Guid.NewGuid();
    private static readonly Guid Spf30 = Guid.NewGuid();
    private static readonly Guid Sudocrem = Guid.NewGuid();
    private static string PagePath => $"/HeadOffice/Customers/Detail/{Hickeys}";

    [Fact]
    public async Task Should_ShowTheTiersAndASampleOfPrices_When_HeadOfficeOpensTheCustomer()
    {
        using PricingReplyHandler api = new();
        await using StaffWebsiteFactory website = new(api);
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);

        string html = await PageAsync(browser, PagePath);

        Assert.Contains("<p class=\"customer-tiers\">Tiers: Suncare Deal 2027, Tier B</p>", html);
        Assert.Matches($"value=\"{TierB}\" checked=\"checked\"", html);
        Assert.Matches($"value=\"{Deal}\" checked=\"checked\"", html);
        Assert.DoesNotMatch($"value=\"{TierC}\" checked", html);
        Assert.Contains("Tier C · 5% off base", html);
        string row = SampleRow(html, Spf30);
        Assert.Contains("SPF30 Sun Lotion 200ml", row);
        Assert.Contains("<td>€12.50</td>", row);
        Assert.Contains("<td>€10.00</td>", row);
        Assert.Contains("<td>Suncare Deal 2027</td>", row);
        Assert.DoesNotContain("aren't saved yet", html);
        Assert.Equal(new[] { TierB, Deal }.Order(), api.SampledWith!.Order());
    }

    [Fact]
    public async Task Should_RecalculateTheSampleWithoutSaving_When_TiersAreTicked()
    {
        using PricingReplyHandler api = new();
        await using StaffWebsiteFactory website = new(api);
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        string html = await PageAsync(browser, PagePath);

        // Untick the deal, tick Tier C, check Sudocrem: the sample is worked out with those tiers and nothing is saved.
        using HttpResponseMessage updated = await PostAsync(browser, PagePath + "?handler=Sample", html,
            [("TierIds", TierB.ToString()), ("TierIds", TierC.ToString()), ("Find", "sudo")]);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        string sample = WebUtility.HtmlDecode(await updated.Content.ReadAsStringAsync());
        Assert.Equal(new[] { TierB, TierC }.Order(), api.SampledWith!.Order());
        Assert.Equal("sudo", api.SampledFind);
        Assert.Null(api.Saved);
        Assert.Contains("The sample shows the ticked tiers. They aren't saved yet.", sample);
        Assert.Contains("Tiers: Suncare Deal 2027, Tier B", sample); // Still what's saved.
        Assert.Matches($"value=\"{TierC}\" checked=\"checked\"", sample);
        Assert.DoesNotMatch($"value=\"{Deal}\" checked", sample);
        Assert.Contains("1 product matches “sudo”", sample);
        Assert.Contains("<td>Tier C</td>", SampleRow(sample, Sudocrem));

        using HttpResponseMessage saved = await PostAsync(browser, PagePath + "?handler=Tiers", sample,
            [("TierIds", TierB.ToString()), ("TierIds", TierC.ToString())]);
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.Equal(new[] { TierB, TierC }.Order(), api.Saved!.Order());
    }

    private static string SampleRow(string html, Guid product)
    {
        int start = html.IndexOf($"data-sample-product=\"{product}\"", StringComparison.Ordinal);
        return html[start..html.IndexOf("</tr>", start, StringComparison.Ordinal)];
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

    private static Task<HttpResponseMessage> PostAsync(HttpClient browser, string path, string html, List<(string Name, string Value)> form)
    {
        Match token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(token.Success);
        form.Add(("__RequestVerificationToken", WebUtility.HtmlDecode(token.Groups[1].Value)));
        return browser.PostAsync(path, new FormUrlEncodedContent(form.Select(field => KeyValuePair.Create(field.Name, field.Value))));
    }

    private sealed class PricingReplyHandler : HttpMessageHandler
    {
        public Guid[]? SampledWith { get; private set; }
        public string? SampledFind { get; private set; }
        public Guid[]? Saved { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("test-access-token", request.Headers.Authorization!.Parameter);
            string path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get && path == $"/directory/customers/{Hickeys}")
                return Json(new CustomerDetails(Hickeys, "Hickey's Pharmacies", "AAAAAAAAB9E=",
                [
                    new LocationSummary(Guid.NewGuid(), "Hickey's Arklow", new TownChoice(Guid.NewGuid(), "Arklow", Guid.NewGuid(), "Wicklow",
                        Guid.NewGuid(), "Leinster", "Arklow, Co. Wicklow"), null, "AAAAAAAAB9E=")
                ]));
            if (request.Method == HttpMethod.Get && path == $"/pricing/customers/{Hickeys}/tiers")
                return Json(new CustomerTiersDetails(Hickeys, [TierB, Deal],
                [
                    new PriceTierListItem(Deal, "Suncare Deal 2027", null, 20, PriceTierShapes.PriceList),
                    new PriceTierListItem(TierB, "Tier B", 8m, 2, PriceTierShapes.Percentage),
                    new PriceTierListItem(TierC, "Tier C", 5m, 0, PriceTierShapes.Percentage)
                ]));
            if (request.Method == HttpMethod.Get && path == $"/pricing/customers/{Hickeys}/sample")
            {
                var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query);
                SampledWith = [.. (query.GetValues("tierId") ?? []).Select(Guid.Parse)];
                SampledFind = query["find"];
                CustomerPriceSampleRow[] found = SampledFind is null ? []
                    : [new(Sudocrem, "SUDO-125", "Sudocrem 125g", "Each", 1m, 4.85m, 4.61m, "Tier C")];
                return Json(new CustomerPriceSample(
                    [new(Spf30, "SUN-030", "SPF30 Sun Lotion 200ml", "Each", 1m, 12.50m, 10.00m, "Suncare Deal 2027")], found, found.Length));
            }
            if (request.Method == HttpMethod.Put && path == $"/pricing/customers/{Hickeys}/tiers")
            {
                Saved = [.. (await request.Content!.ReadFromJsonAsync<SetCustomerTiersRequest>(cancellationToken))!.TierIds!];
                return new(HttpStatusCode.NoContent);
            }
            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        }

        private static HttpResponseMessage Json<T>(T body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };
    }
}
