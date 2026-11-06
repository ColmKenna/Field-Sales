using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FieldSales.Web.Security;

namespace FieldSales.Web.Tests;

/// <summary>WI-062 agreed scenarios 5 and 12: the product record's "Quantity breaks" section (H-22) against scripted API replies.
/// The rules and storage are covered by QuantityBreakApiTests; the section end to end by QuantityBreakIntegrationTests.</summary>
[Trait("Category", "Integration")]
public sealed class QuantityBreakPageTests
{
    [Fact]
    public async Task Should_ListBreaksByQuantityBelowTheBasePrice_When_HeadOfficeOpensTheProduct()
    {
        using BreakReplyHandler api = new();
        await using StaffWebsiteFactory website = new(api);
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);

        string html = await PageAsync(browser, api.Path);

        string section = Section(html);
        Assert.Contains("<p class=\"breaks-base\">Base price €2.20 each</p>", section);
        Assert.True(section.IndexOf("breaks-base", StringComparison.Ordinal) < section.IndexOf("data-break=\"0\"", StringComparison.Ordinal));
        Assert.Contains("name=\"Breaks[0].FromQuantity\" type=\"number\" min=\"1\" step=\"1\"", Row(section, "0"));
        Assert.Contains("value=\"10\"", Row(section, "0"));
        Assert.Contains("value=\"2.00\"", Row(section, "0"));
        Assert.Contains("value=\"30\"", Row(section, "1"));
        Assert.DoesNotContain("will never apply", Row(section, "0"));
        Assert.Contains("This break is higher than the one below it and will never apply.", Row(section, "1"));
        Assert.Contains("This break is above the base price and will never apply.", Row(section, "1"));
        Assert.Contains("Add a break", Row(section, "new"));
        Assert.Contains("name=\"Breaks[2].FromQuantity\"", Row(section, "new"));
    }

    [Fact]
    public async Task Should_EditBreaksInPlace_When_HeadOfficeUsesTheSection()
    {
        using BreakReplyHandler api = new();
        await using StaffWebsiteFactory website = new(api);
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        string html = await PageAsync(browser, api.Path);

        // Change 30 to €1.75 and add 50 at €1.60 in the blank row.
        using HttpResponseMessage saved = await PostAsync(browser, api.Path + "?handler=Breaks", html, Rows(("10", "2.00"), ("30", "1.75"), ("50", "1.60")));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.Equal([(10m, 2.00m), (30m, 1.75m), (50m, 1.60m)], api.Sent!);

        // Remove the second row; the blank add row is left out.
        Dictionary<string, string> removing = Rows(("10", "2.00"), ("30", "2.30"), ("", ""));
        removing["remove"] = "1";
        using HttpResponseMessage removed = await PostAsync(browser, api.Path + "?handler=Breaks", html, removing);
        Assert.Equal(HttpStatusCode.Redirect, removed.StatusCode);
        Assert.Equal([(10m, 2.00m)], api.Sent!);

        // A refusal for the second break sent lands on the form row it came from, which keeps what was typed.
        api.Refusal = new() { ["Breaks[1].FromQuantity"] = ["Enter a whole number."] };
        using HttpResponseMessage refused = await PostAsync(browser, api.Path + "?handler=Breaks", html, Rows(("", ""), ("10", "2.00"), ("12.5", "1.90")));
        Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
        Assert.Equal([(10m, 2.00m), (12.5m, 1.90m)], api.Sent!);
        string section = Section(WebUtility.HtmlDecode(await refused.Content.ReadAsStringAsync()));
        Assert.Contains("Enter a whole number.", Row(section, "2"));
        Assert.Contains("value=\"12.5\"", Row(section, "2"));
        Assert.DoesNotContain("Enter a whole number.", Row(section, "1"));
    }

    private static Dictionary<string, string> Rows(params (string From, string Price)[] rows)
    {
        Dictionary<string, string> form = [];
        for (int index = 0; index < rows.Length; index++)
        {
            form[$"Breaks[{index}].FromQuantity"] = rows[index].From;
            form[$"Breaks[{index}].Price"] = rows[index].Price;
        }
        return form;
    }

    private static string Section(string html)
    {
        int start = html.IndexOf("id=\"breaks-heading\"", StringComparison.Ordinal);
        return html[start..html.IndexOf("</section>", start, StringComparison.Ordinal)];
    }

    // One row of the section, up to the next row.
    private static string Row(string section, string row)
    {
        int start = section.IndexOf($"data-break=\"{row}\"", StringComparison.Ordinal);
        int next = section.IndexOf("data-break=", start + 1, StringComparison.Ordinal);
        return section[start..(next < 0 ? section.Length : next)];
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

    private sealed class BreakReplyHandler : HttpMessageHandler
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string Path => $"/HeadOffice/Products/{Id}";
        public Dictionary<string, string[]>? Refusal { get; set; }
        public (decimal From, decimal Price)[]? Sent { get; private set; }

        private ProductDetails Details => new(new ProductItem(Id, "LOZ-36", "Throat Lozenges 36s", Guid.NewGuid(), "Each"), [],
            new ProductPriceItem(new DateOnly(2026, 9, 1), 2.20m), [new ProductPriceItem(new DateOnly(2026, 9, 1), 2.20m)], [],
            QuantityBreaks: [new ProductQuantityBreakItem(30m, 2.30m, true, true), new ProductQuantityBreakItem(10m, 2.00m, false, false)]);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("test-access-token", request.Headers.Authorization!.Parameter);
            string path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get && path == $"/catalogue/products/{Id}")
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(Details) };
            if (request.Method == HttpMethod.Put && path == $"/catalogue/products/{Id}/quantity-breaks")
            {
                SetProductQuantityBreaksRequest body = (await request.Content!.ReadFromJsonAsync<SetProductQuantityBreaksRequest>(cancellationToken))!;
                Sent = [.. body.Breaks!.Select(entry => (entry.FromQuantity!.Value, entry.Price!.Value))];
                return Refusal is null
                    ? new(HttpStatusCode.NoContent)
                    : new(HttpStatusCode.BadRequest) { Content = JsonContent.Create(new ProductValidationErrors(Refusal)) };
            }
            throw new InvalidOperationException($"Unexpected catalogue request: {request.Method} {request.RequestUri}");
        }
    }
}
