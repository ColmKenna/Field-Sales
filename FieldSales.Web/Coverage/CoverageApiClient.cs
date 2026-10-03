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

    public Task<DirectoryResult<AssignmentReviewOptions>> OptionsAsync(string? rep, CancellationToken ct) =>
        SendAsync<AssignmentReviewOptions>(HttpMethod.Get, "/coverage/assignment-options?repSubject=" + Uri.EscapeDataString(rep ?? ""), null, ct);
    public Task<DirectoryResult<AssignmentImpactDetails>> PreviewAddAsync(AddTerritoryAssignmentRequest value, CancellationToken ct) =>
        SendAsync<AssignmentImpactDetails>(HttpMethod.Post, "/coverage/assignments/preview", value, ct);
    public Task<DirectoryResult<AssignmentImpactDetails>> PreviewRemoveAsync(Guid id, RemoveTerritoryAssignmentRequest value, CancellationToken ct) =>
        SendAsync<AssignmentImpactDetails>(HttpMethod.Post, $"/coverage/assignments/{id}/remove/preview", value, ct);
    public Task<DirectoryResult<AssignmentSaveResult>> SaveAddAsync(AddTerritoryAssignmentRequest value, CancellationToken ct) =>
        SaveAsync("/coverage/assignments", value, ct);
    public Task<DirectoryResult<AssignmentSaveResult>> SaveRemoveAsync(Guid id, RemoveTerritoryAssignmentRequest value, CancellationToken ct) =>
        SaveAsync($"/coverage/assignments/{id}/remove", value, ct);

    // Writes are never retried automatically. A 409 may carry the new preview,
    // which the page renders with another explicit confirmation action.
    private async Task<DirectoryResult<AssignmentSaveResult>> SaveAsync(string path, object value, CancellationToken ct)
    {
        string? token = contexts.HttpContext is { } context ? await context.GetTokenAsync("access_token") : null;
        if (string.IsNullOrWhiteSpace(token)) return new(HttpStatusCode.Unauthorized);
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(value) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        try
        {
            using var response = await client.SendAsync(request, ct);
            if (response.IsSuccessStatusCode) return new(response.StatusCode, new(true));
            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
            {
                var error = await response.Content.ReadFromJsonAsync<CoverageError>(ct);
                if (error?.Preview is { } preview && !ValidPreview(preview)) return new(HttpStatusCode.ServiceUnavailable);
                return new(response.StatusCode, new(false, error?.Preview), error?.Error, error?.Field);
            }
            return new(response.StatusCode);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or NotSupportedException or HttpRequestException
            or Polly.Timeout.TimeoutRejectedException or Polly.CircuitBreaker.BrokenCircuitException)
        { return new(HttpStatusCode.ServiceUnavailable); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return new(HttpStatusCode.ServiceUnavailable); }
    }

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
                AssignmentImpactDetails preview => ValidPreview(preview),
                AssignmentReviewOptions options => options.Reps is not null && options.Targets is not null && options.Assignments is not null
                    && options.Reps.All(ValidChoice) && options.Targets.All(row => row is not null && row.Target is not null
                        && Enum.IsDefined(row.Target.Level) && row.Target.UnitId != Guid.Empty && !string.IsNullOrWhiteSpace(row.Name)
                        && !string.IsNullOrWhiteSpace(row.Label))
                    && options.Assignments.All(row => row is not null && row.Assignment is not null
                        && row.Assignment.Id != Guid.Empty && !string.IsNullOrWhiteSpace(row.Name) && !string.IsNullOrWhiteSpace(row.Assignment.Version)),
                _ => false
            };
            return valid ? new(response.StatusCode, result) : new(HttpStatusCode.ServiceUnavailable);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or NotSupportedException
            or HttpRequestException or Polly.Timeout.TimeoutRejectedException or Polly.CircuitBreaker.BrokenCircuitException)
        { return new(HttpStatusCode.ServiceUnavailable); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return new(HttpStatusCode.ServiceUnavailable); }
    }
    private static bool ValidPreview(AssignmentImpactDetails value) => !string.IsNullOrWhiteSpace(value.Proof)
        && value.Action is "Add" or "Remove" && !string.IsNullOrWhiteSpace(value.TargetName) && !string.IsNullOrWhiteSpace(value.RepName)
        && value.ChangedLocations >= 0 && value.Groups is not null
        && value.Groups.All(group => group is not null && !string.IsNullOrWhiteSpace(group.Sentence) && group.Locations is not null
            && group.Locations.Count > 0 && group.Locations.All(row => row is not null && row.LocationId != Guid.Empty
                && !string.IsNullOrWhiteSpace(row.Name) && ValidOwner(row.PreviousOwner) && ValidOwner(row.NewOwner)))
        && value.Groups.Sum(group => group.Locations.Count) == value.ChangedLocations
        && value.Groups.SelectMany(group => group.Locations).Select(row => row.LocationId).Distinct().Count() == value.ChangedLocations;
    private static bool ValidOwner(ImpactOwnerDetails? owner) => owner is null || ValidChoice(owner.Rep)
        && owner.Source is not null && Enum.IsDefined(owner.Source.Level) && owner.Source.UnitId != Guid.Empty && !string.IsNullOrWhiteSpace(owner.SourceName);
    private static bool ValidChoice(StaffChoice? value) => value is not null && !string.IsNullOrWhiteSpace(value.Subject) && !string.IsNullOrWhiteSpace(value.Name);
    private static bool ValidLine(RepReportingLineDetails? value) => value is not null && !string.IsNullOrWhiteSpace(value.RepSubject)
        && !string.IsNullOrWhiteSpace(value.ManagerSubject) && !string.IsNullOrWhiteSpace(value.Version);
}

public sealed record AssignmentSaveResult(bool Saved, AssignmentImpactDetails? Preview = null);
