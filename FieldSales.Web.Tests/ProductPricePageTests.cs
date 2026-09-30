using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FieldSales.Web.Catalogue;
using FieldSales.Web.Data;
using FieldSales.Web.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FieldSales.Web.Tests;

public sealed class ProductPricePageTests
{
    [Fact]
    public async Task Should_KeepUnitForm_When_ProductRecordIsOpened()
    {
        using PriceReplyHandler api = new();
        await using StaffWebsiteFactory website = new(api);
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        string html = await PageAsync(browser, api.Path);
        Assert.Contains("€12.50 per kg", html);
        Assert.Contains("Effective from 1 Sept 2026", html);
        Assert.Contains("value=\"kg\" selected=\"selected\"", html);
        Assert.Contains("name=\"QuantityStep\"", html);
        Assert.Contains("value=\"0.5\"", html);
        Assert.Contains("No profile yet.", html);
    }

    [Fact]
    public async Task Should_ShowComingRise_When_FuturePriceIsAdded()
    {
        using PriceReplyHandler api = new();
        ProductPriceItem initial = api.Details.CurrentPrice!;
        ProductPriceItem november = new(new DateOnly(2026, 11, 1), 13.20m);
        ProductPriceItem december = new(new DateOnly(2026, 12, 1), 14m);
        api.Details = api.Details with { PriceHistory = [initial, december] };
        api.AfterSave = api.Details with { PriceHistory = [initial, november, december] };
        await using StaffWebsiteFactory website = new(api);
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        string form = await PageAsync(browser, api.Path);
        Assert.Contains("rising to €14.00 on 1 Dec", form);
        Assert.Contains("name=\"EffectiveFrom\"", form);
        using HttpResponseMessage saved = await PostPriceAsync(browser, api.Path, form);
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.Equal(api.Path, saved.Headers.Location!.OriginalString);
        Assert.Equal(new SubmittedPrice(13.20m, november.EffectiveFrom), api.Submitted);
        string record = await PageAsync(browser, api.Path);
        Assert.Contains("€12.50 · rising to €13.20 on 1 Nov", record);
        Assert.DoesNotContain("rising to €14.00", record);
        Assert.True(record.IndexOf("datetime=\"2026-12-01\"", StringComparison.Ordinal)
            < record.IndexOf("datetime=\"2026-11-01\"", StringComparison.Ordinal));
        Assert.True(record.IndexOf("datetime=\"2026-11-01\"", StringComparison.Ordinal)
            < record.IndexOf("datetime=\"2026-09-01\"", StringComparison.Ordinal));
        Assert.Equal(1, api.SaveCount);
    }

    [Fact]
    public async Task Should_UseNewPrice_When_EffectiveDateIsToday()
    {
        using PriceReplyHandler api = new();
        ProductPriceItem today = new(new DateOnly(2026, 10, 1), 13.20m);
        api.AfterSave = api.Details with { CurrentPrice = today, PriceHistory = [today, api.Details.CurrentPrice!] };
        await using StaffWebsiteFactory website = new(api);
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        using HttpResponseMessage saved = await PostPriceAsync(browser, api.Path,
            await PageAsync(browser, api.Path), date: "2026-10-01");
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        string record = await PageAsync(browser, api.Path);
        Assert.Contains("€13.20 per kg · Effective from 1 Oct 2026", record);
        Assert.DoesNotContain("rising to", record);
    }

    [Fact]
    public async Task Should_ShowPastPriceAndCaptureNote_When_PastEntryIsAdded()
    {
        using PriceReplyHandler api = new();
        ProductPriceItem august = new(new DateOnly(2026, 8, 1), 12.50m);
        ProductPriceItem september = new(new DateOnly(2026, 9, 1), 12m);
        ProductPriceItem november = new(new DateOnly(2026, 11, 1), 13.20m);
        api.Details = api.Details with { CurrentPrice = august, PriceHistory = [august, november] };
        api.AfterSave = api.Details with { CurrentPrice = september, PriceHistory = [august, september, november] };
        await using StaffWebsiteFactory website = new(api);
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        string form = await PageAsync(browser, api.Path);
        Assert.Contains("Existing orders keep the price captured", form);
        using HttpResponseMessage saved = await PostPriceAsync(browser, api.Path, form, "12.00", "2026-09-01");
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.Equal(new SubmittedPrice(12m, september.EffectiveFrom), api.Submitted);
        string record = await PageAsync(browser, api.Path);
        Assert.Contains("€12.00 · rising to €13.20 on 1 Nov", record);
        Assert.Contains("Existing orders keep the price captured", record);
        Assert.Contains("datetime=\"2026-08-01\"", record);
        Assert.Contains("datetime=\"2026-09-01\"", record);
        Assert.Contains("datetime=\"2026-11-01\"", record);
        Assert.Equal(1, api.SaveCount);
    }

    [Fact]
    public async Task Should_RejectDuplicateDate_When_PriceAlreadyStartsThatDay()
    {
        using PriceReplyHandler api = new();
        const string error = "A price already starts on 1 Nov 2026 — edit it instead";
        api.SaveStatus = HttpStatusCode.Conflict;
        api.SaveBody = new { Field = "EffectiveFrom", Error = error };
        api.Details = api.Details with { PriceHistory = [new(new DateOnly(2026, 11, 1), 13.20m), api.Details.CurrentPrice!] };
        await using StaffWebsiteFactory website = new(api);
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        using HttpResponseMessage rejected = await PostPriceAsync(browser, api.Path, await PageAsync(browser, api.Path), "14.00");
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        string html = WebUtility.HtmlDecode(await rejected.Content.ReadAsStringAsync());
        Assert.Contains(error, html);
        Assert.Contains("value=\"14.00\"", html);
        Assert.Contains("value=\"2026-11-01\"", html);
        Assert.Contains("€12.50 · rising to €13.20 on 1 Nov", html);
        Assert.Contains("value=\"kg\" selected=\"selected\"", html);
        Assert.Equal(2, api.Details.PriceHistory.Count);
    }

    [Theory]
    [InlineData("BasePrice", "Enter a base price.")]
    [InlineData("EffectiveFrom", "Enter an effective from date.")]
    public async Task Should_ShowFieldError_When_ApiRejectsPrice(string field, string error)
    {
        using PriceReplyHandler api = new();
        api.SaveStatus = HttpStatusCode.BadRequest;
        api.SaveBody = new { Errors = new Dictionary<string, string[]> { [field] = [error] } };
        await using StaffWebsiteFactory website = new(api);
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        using HttpResponseMessage rejected = await PostPriceAsync(browser, api.Path, await PageAsync(browser, api.Path),
            field == "BasePrice" ? "" : "13.20", field == "EffectiveFrom" ? "" : "2026-11-01");
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        string html = WebUtility.HtmlDecode(await rejected.Content.ReadAsStringAsync());
        Assert.Contains($"data-valmsg-for=\"{field}\"", html);
        Assert.Contains(error, html);
        Assert.Contains("€12.50 per kg", html);
        Assert.Single(api.Details.PriceHistory);
    }

    [Fact]
    public async Task Should_ShowFirstComingPrice_When_NoPriceIsInEffect()
    {
        using PriceReplyHandler api = new();
        api.Details = api.Details with { CurrentPrice = null, PriceHistory =
            [new(new DateOnly(2026, 12, 1), 14m), new(new DateOnly(2026, 11, 1), 13.20m)] };
        await using StaffWebsiteFactory website = new(api);
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        string html = await PageAsync(browser, api.Path);
        Assert.Contains("No price in effect.", html);
        Assert.Contains("Starting at €13.20 on 1 Nov 2026", html);
        Assert.DoesNotContain("Starting at €14.00", html);
    }

    [Theory]
    [InlineData("not-an-amount", "2026-11-01")]
    [InlineData("13.20", "not-a-date")]
    public async Task Should_RetainEnteredValuesWithoutApiWrite_When_InputCannotBeBound(string amount, string date)
    {
        using PriceReplyHandler api = new();
        await using StaffWebsiteFactory website = new(api);
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        using HttpResponseMessage rejected = await PostPriceAsync(browser, api.Path,
            await PageAsync(browser, api.Path), amount, date);
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        string html = WebUtility.HtmlDecode(await rejected.Content.ReadAsStringAsync());
        Assert.Contains($"value=\"{amount}\"", html);
        Assert.Contains($"value=\"{date}\"", html);
        Assert.Contains("field-validation-error", html);
        Assert.Equal(0, api.SaveCount);
    }

    [Theory]
    [InlineData("12.00", "falling to")]
    [InlineData("12.50", "changing to")]
    public async Task Should_DescribeComingChange_When_PriceIsNotRising(string amount, string direction)
    {
        using PriceReplyHandler api = new();
        api.Details = api.Details with { PriceHistory = [api.Details.CurrentPrice!,
            new(new DateOnly(2026, 11, 1), decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture))] };
        await using StaffWebsiteFactory website = new(api);
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        string html = await PageAsync(browser, api.Path);
        Assert.Contains($"€12.50 · {direction} €{amount} on 1 Nov", html);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_DenyPriceWriteWithoutReplay_When_RoleIsRemovedOrSessionExpires(bool expired)
    {
        using PriceReplyHandler api = new();
        await using StaffWebsiteFactory website = new(api);
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        string form = await PageAsync(browser, api.Path);
        if (expired)
        {
            await using AsyncServiceScope scope = website.Services.CreateAsyncScope();
            StaffWebDbContext db = scope.ServiceProvider.GetRequiredService<StaffWebDbContext>();
            StoredTicket ticket = await db.Tickets.SingleAsync();
            ticket.ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }
        else website.Roles.SetRoles("niamh");
        using HttpResponseMessage denied = await PostPriceAsync(browser, api.Path, form);
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.StartsWith(expired ? "https://localhost:7201/connect/authorize" : "/AccessChanged?state=",
            denied.Headers.Location!.OriginalString);
        Assert.Equal(0, api.SaveCount);
        await SignInAsync(browser);
        Assert.Contains("€12.50 per kg", await PageAsync(browser, api.Path));
        Assert.Equal(0, api.SaveCount);
    }

    [Theory]
    [InlineData(StaffRoles.FieldSalesperson)]
    [InlineData(StaffRoles.SalesManager)]
    public async Task Should_DenyPriceReadAndWrite_When_HeadOfficeRoleIsMissing(string role)
    {
        using PriceReplyHandler api = new();
        await using StaffWebsiteFactory website = new(api);
        using HttpClient browser = website.CreateBrowser();
        await SignInAsync(browser);
        string form = await PageAsync(browser, api.Path);
        using HttpClient otherStaff = website.CreateBrowser();
        await SignInAsync(otherStaff, role);
        using HttpResponseMessage deniedRead = await otherStaff.GetAsync(api.Path);
        Assert.Equal(HttpStatusCode.Redirect, deniedRead.StatusCode);
        Assert.Equal("/AccessDenied", deniedRead.Headers.Location!.AbsolutePath);
        website.Roles.SetRoles("niamh", role);
        using HttpResponseMessage deniedWrite = await PostPriceAsync(browser, api.Path, form);
        Assert.StartsWith("/AccessChanged?state=", deniedWrite.Headers.Location!.OriginalString);
        Assert.Equal(0, api.SaveCount);
    }

    private static async Task SignInAsync(HttpClient browser, string role = StaffRoles.HeadOfficeUser)
    {
        using HttpResponseMessage response = await browser.GetAsync(
            $"/__test/sign-in?subject=niamh&roles={Uri.EscapeDataString(role)}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static async Task<string> PageAsync(HttpClient browser, string path)
    {
        using HttpResponseMessage response = await browser.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }

    private static Task<HttpResponseMessage> PostPriceAsync(HttpClient browser, string path, string html,
        string amount = "13.20", string date = "2026-11-01")
    {
        Match token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(token.Success);
        return browser.PostAsync(path + "?handler=Price", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["BasePrice"] = amount, ["EffectiveFrom"] = date,
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups[1].Value)
        }));
    }

    // Scripted API responses exercise the real Razor page, BFF, cookie and antiforgery boundary.
    // Business validation and actual SQL persistence are covered by ProductPriceApiTests.
    private sealed class PriceReplyHandler : HttpMessageHandler
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string Path => $"/HeadOffice/Products/{Id}";
        public ProductDetails Details { get; set; }
        public ProductDetails? AfterSave { get; set; }
        public HttpStatusCode SaveStatus { get; set; } = HttpStatusCode.Created;
        public object SaveBody { get; set; } = new { };
        public int SaveCount { get; private set; }
        public SubmittedPrice? Submitted { get; private set; }

        public PriceReplyHandler()
        {
            ProductPriceItem initial = new(new DateOnly(2026, 9, 1), 12.50m);
            Details = new(new ProductItem(Id, "SUN-0342", "Lotion", Guid.NewGuid(), "kg", 0.5m, 1m),
                [], initial, [initial], []);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("test-access-token", request.Headers.Authorization.Parameter);
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath == $"/catalogue/products/{Id}")
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(Details) };
            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath == $"/catalogue/products/{Id}/base-prices")
            {
                SaveCount++;
                Submitted = await request.Content!.ReadFromJsonAsync<SubmittedPrice>(cancellationToken);
                if (SaveStatus == HttpStatusCode.Created && AfterSave is not null) Details = AfterSave;
                return new(SaveStatus) { Content = JsonContent.Create(SaveBody) };
            }
            throw new InvalidOperationException($"Unexpected catalogue request: {request.Method} {request.RequestUri}");
        }
    }

    private sealed record SubmittedPrice(decimal? BasePrice, DateOnly? EffectiveFrom);
}
