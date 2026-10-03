using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FieldSales.Directory.Contracts;
using FieldSales.Web.Directory;
using Microsoft.AspNetCore.Authentication;

namespace FieldSales.Web.Coverage;

public sealed class CoverageApiClient(HttpClient client, IHttpContextAccessor contexts)
{
    public Task<DirectoryResult<ReportingLinesPage>> ReportingLinesAsync(CancellationToken ct) =>
        SendAsync<ReportingLinesPage>(HttpMethod.Get, "/coverage/reporting-lines", null, ct);
    public Task<DirectoryResult<RepReportingLineDetails>> SetReportingLineAsync(string rep, SetRepReportingLineRequest value, CancellationToken ct) =>
        SendAsync<RepReportingLineDetails>(HttpMethod.Put, "/coverage/reporting-lines/" + Uri.EscapeDataString(rep), value, ct);

    private async Task<DirectoryResult<T>> SendAsync<T>(HttpMethod method, string path, object? value, CancellationToken ct)
    {
        string? token = contexts.HttpContext is { } context ? await context.GetTokenAsync("access_token") : null;
        if (string.IsNullOrWhiteSpace(token)) return new(HttpStatusCode.Unauthorized);
        using var request = new HttpRequestMessage(method, path) { Content = value is null ? null : JsonContent.Create(value) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        try
        {
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
                {
                    var error = await response.Content.ReadFromJsonAsync<CoverageError>(ct);
                    return new(response.StatusCode, Error: error?.Error ?? "This change could not be saved.", Field: error?.Field);
                }
                return new(response.StatusCode);
            }
            var result = await response.Content.ReadFromJsonAsync<T>(ct);
            bool valid = result switch
            {
                ReportingLinesPage page => page.Reps is not null && page.Managers is not null && page.Lines is not null
                    && page.Reps.All(ValidChoice) && page.Managers.All(ValidChoice)
                    && page.Lines.All(row => row is not null && ValidLine(row.Line) && !string.IsNullOrWhiteSpace(row.RepName)
                        && !string.IsNullOrWhiteSpace(row.ManagerName)),
                RepReportingLineDetails line => ValidLine(line),
                _ => false
            };
            return valid ? new(response.StatusCode, result) : new(HttpStatusCode.ServiceUnavailable);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or NotSupportedException
            or HttpRequestException or Polly.Timeout.TimeoutRejectedException or Polly.CircuitBreaker.BrokenCircuitException)
        { return new(HttpStatusCode.ServiceUnavailable); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return new(HttpStatusCode.ServiceUnavailable); }
    }
    private static bool ValidChoice(StaffChoice? value) => value is not null && !string.IsNullOrWhiteSpace(value.Subject) && !string.IsNullOrWhiteSpace(value.Name);
    private static bool ValidLine(RepReportingLineDetails? value) => value is not null && !string.IsNullOrWhiteSpace(value.RepSubject)
        && !string.IsNullOrWhiteSpace(value.ManagerSubject) && !string.IsNullOrWhiteSpace(value.Version);
}
