using System.Net;
using FieldSales.StaffAccess;

namespace FieldSales.Web.Tests;

public sealed class TestReadResilienceTests
{
    [Fact]
    public async Task FailedPageReadsStillRetryBeforeReturningUnavailable()
    {
        using var handler = new Unavailable();
        await using var website = new StaffWebsiteFactory(handler);
        using var browser = website.CreateBrowser();
        using var login = await browser.GetAsync("/__test/sign-in?subject=manager&roles=" +
            Uri.EscapeDataString(BusinessRoles.SalesManager));
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        using var response = await browser.GetAsync($"/Coverage/Location/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(4, handler.Calls); // Initial GET plus the standard policy's three retries.
    }

    private sealed class Unavailable : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }
    }
}
