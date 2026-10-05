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
    public Task<DirectoryResult<UnassignedCoveragePage>> UnassignedAsync(CancellationToken ct) =>
        SendAsync<UnassignedCoveragePage>(HttpMethod.Get, "/coverage/unassigned", null, ct);
    public async Task<DirectoryResult<LocationCoverageActions>> LocationActionsAsync(Guid id, CancellationToken ct)
    {
        var result = await SendAsync<LocationCoverageActions>(HttpMethod.Get, $"/coverage/locations/{id}/page/actions", null, ct);
        return result.Success && result.Value!.Location.LocationId != id ? new(HttpStatusCode.ServiceUnavailable) : result;
    }
    public async Task<DirectoryResult<LocationCoveragePage>> LocationAsync(Guid id, CancellationToken ct)
    {
        var result = await SendAsync<LocationCoveragePage>(HttpMethod.Get, $"/coverage/locations/{id}/page", null, ct);
        return result.Success && result.Value!.LocationId != id ? new(HttpStatusCode.ServiceUnavailable) : result;
    }
    public async Task<DirectoryResult<LocationCoverageHistoryPage>> LocationHistoryAsync(Guid id, CancellationToken ct)
    {
        var result = await SendAsync<LocationCoverageHistoryPage>(HttpMethod.Get, $"/coverage/locations/{id}/page/history", null, ct);
        return result.Success && result.Value!.LocationId != id ? new(HttpStatusCode.ServiceUnavailable) : result;
    }
    public async Task<DirectoryResult<RepTerritoryPage>> TerritoryAsync(string rep, CancellationToken ct)
    {
        var result = await SendAsync<RepTerritoryPage>(HttpMethod.Get, "/coverage/reps/" + Uri.EscapeDataString(rep) + "/territory", null, ct);
        return result.Success && result.Value!.Rep.Subject != rep ? new(HttpStatusCode.ServiceUnavailable) : result;
    }
    public async Task<DirectoryResult<TransferReview>> TransferReviewAsync(string source, CancellationToken ct)
    {
        var result = await SendAsync<TransferReview>(HttpMethod.Get, "/coverage/transfers/review?sourceRepSubject=" + Uri.EscapeDataString(source), null, ct);
        return result.Success && result.Value!.SourceRep.Subject != source ? new(HttpStatusCode.ServiceUnavailable) : result;
    }
    public async Task<DirectoryResult<TransferSourceOptions>> TransferSourcesAsync(string recipient, CancellationToken ct)
    {
        var result = await SendAsync<TransferSourceOptions>(HttpMethod.Get, "/coverage/transfers/sources?receivingRepSubject=" + Uri.EscapeDataString(recipient), null, ct);
        return result.Success && result.Value!.ReceivingRep.Subject != recipient ? new(HttpStatusCode.ServiceUnavailable) : result;
    }
    public async Task<DirectoryResult<AssignmentImpactDetails>> PreviewTransferAsync(TransferAssignmentsRequest value, CancellationToken ct)
    {
        var result = await SendAsync<AssignmentImpactDetails>(HttpMethod.Post, "/coverage/transfers/preview", value, ct);
        return result.Success && result.Value!.Action != "Transfer" ? new(HttpStatusCode.ServiceUnavailable) : result;
    }
    public Task<DirectoryResult<AssignmentSaveResult>> SaveTransferAsync(TransferAssignmentsRequest value, CancellationToken ct) =>
        SaveAsync("/coverage/transfers", value, ct, true);

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
    private async Task<DirectoryResult<AssignmentSaveResult>> SaveAsync(string path, object value, CancellationToken ct, bool transfer = false)
    {
        string? token = contexts.HttpContext is { } context ? await context.GetTokenAsync("access_token") : null;
        if (string.IsNullOrWhiteSpace(token)) return new(HttpStatusCode.Unauthorized);
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(value) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        try
        {
            using var response = await client.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
            {
                if (transfer)
                {
                    var saved = await response.Content.ReadFromJsonAsync<CoverageMutationResult>(ct);
                    if (saved is null || !saved.Saved || saved.ChangedLocations < 0) return new(HttpStatusCode.ServiceUnavailable);
                }
                return new(response.StatusCode, new(true));
            }
            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
            {
                var error = await response.Content.ReadFromJsonAsync<CoverageError>(ct);
                if (error?.Preview is { } preview && (!ValidPreview(preview) || transfer && preview.Action != "Transfer")) return new(HttpStatusCode.ServiceUnavailable);
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
                UnassignedCoveragePage unassigned => ValidUnassigned(unassigned),
                LocationCoverageActions actions => ValidLocationActions(actions),
                LocationCoveragePage location => ValidLocation(location),
                LocationCoverageHistoryPage history => ValidLocationHistory(history),
                RepTerritoryPage territory => ValidTerritory(territory),
                TransferReview review => ValidTransferReview(review),
                TransferSourceOptions sources => ValidChoice(sources.ReceivingRep) && sources.GivingReps is not null
                    && sources.GivingReps.All(row => ValidChoice(row) && row.Subject != sources.ReceivingRep.Subject)
                    && sources.GivingReps.Select(row => row.Subject).Distinct(StringComparer.Ordinal).Count() == sources.GivingReps.Count,
                ReportingLinesPage page => page.Reps is not null && page.Managers is not null && page.Lines is not null
                    && page.Reps.All(ValidChoice) && page.Managers.All(ValidChoice)
                    && page.Lines.All(row => row is not null && ValidLine(row.Line) && !string.IsNullOrWhiteSpace(row.RepName)
                        && !string.IsNullOrWhiteSpace(row.ManagerName)),
                RepReportingLineDetails line => ValidLine(line),
                AssignmentImpactDetails preview => ValidPreview(preview),
                AssignmentReviewOptions options => options.Reps is not null && options.Targets is not null && options.Assignments is not null
                    && options.Reps.All(ValidChoice) && options.Targets.All(row => row is not null && row.Target is not null
                        && Enum.IsDefined(row.Target.Level) && row.Target.UnitId != Guid.Empty && !string.IsNullOrWhiteSpace(row.Name)
                        && !string.IsNullOrWhiteSpace(row.Label) && (row.Holder is null || row.Holder.AssignmentId != Guid.Empty && ValidChoice(row.Holder.Rep)))
                    && options.Targets.Select(row => row.Target).Distinct().Count() == options.Targets.Count
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
        && value.Action is "Add" or "Remove" or "Transfer" && !string.IsNullOrWhiteSpace(value.TargetName) && !string.IsNullOrWhiteSpace(value.RepName)
        && (value.Action != "Transfer" || value.TransferNotices is not null && value.TransferNotices.All(row => !string.IsNullOrWhiteSpace(row))
            && value.TransferredAssignments is { Count: > 0 } && value.TransferredAssignments.All(row => !string.IsNullOrWhiteSpace(row)))
        && value.ChangedLocations >= 0 && value.Groups is not null
        && value.Groups.All(group => group is not null && !string.IsNullOrWhiteSpace(group.Sentence) && group.Locations is not null
            && group.Locations.Count > 0 && group.Locations.All(row => row is not null && row.LocationId != Guid.Empty
                && !string.IsNullOrWhiteSpace(row.Name) && ValidOwner(row.PreviousOwner) && ValidOwner(row.NewOwner)))
        && value.Groups.Sum(group => group.Locations.Count) == value.ChangedLocations
        && value.Groups.SelectMany(group => group.Locations).Select(row => row.LocationId).Distinct().Count() == value.ChangedLocations;
    private static bool ValidLocation(LocationCoveragePage? location) => location is not null
        && location.LocationId != Guid.Empty && !string.IsNullOrWhiteSpace(location.Name)
        && location.TownId != Guid.Empty && !string.IsNullOrWhiteSpace(location.TownName)
        && location.OtherUnassignedLocations >= 0 && ValidOwner(location.Owner)
        && (location.Owner is null || location.Owner.Source.Level switch
        {
            TerritoryLevel.Location => location.Owner.Source.UnitId == location.LocationId,
            TerritoryLevel.Town => location.Owner.Source.UnitId == location.TownId,
            _ => true
        });
    private static bool ValidLocationActions(LocationCoverageActions actions) => ValidLocation(actions.Location)
        && (actions.Location.Owner is null
            ? actions.SourceAssignmentId is null && actions.SourceLocations == 0 && !actions.CanTransferSource
                && actions.CanAssignTown == actions.CanChangeShop
            : actions.SourceAssignmentId is { } id && id != Guid.Empty && actions.SourceLocations > 0
                && !actions.CanAssignTown && (actions.Location.Owner.Source.Level != TerritoryLevel.Location
                    || actions.SourceLocations == 1 && !actions.CanTransferSource));
    private static bool ValidUnassigned(UnassignedCoveragePage page)
    {
        if (page.Towns is null) return false;
        HashSet<Guid> towns = [], locations = [];
        foreach (var town in page.Towns)
        {
            if (town is null || town.Id == Guid.Empty || !towns.Add(town.Id) || string.IsNullOrWhiteSpace(town.Name)
                || string.IsNullOrWhiteSpace(town.CountyName) || string.IsNullOrWhiteSpace(town.RegionName)
                || town.Locations is not { Count: > 0 }) return false;
            foreach (var row in town.Locations)
            {
                if (row is null || row.CustomerId == Guid.Empty || string.IsNullOrWhiteSpace(row.CustomerName)
                    || row.Actions is null || !ValidLocationActions(row.Actions) || row.Actions.Location.Owner is not null
                    || row.Actions.Location.TownId != town.Id || row.Actions.Location.TownName != town.Name
                    || row.Actions.Location.OtherUnassignedLocations != town.Locations.Count - 1
                    || !locations.Add(row.Actions.Location.LocationId)) return false;
            }
        }
        return true;
    }
    private static bool ValidTransferReview(TransferReview value) => ValidChoice(value.SourceRep)
        && value.ReceivingReps is not null && value.ReceivingReps.All(rep => ValidChoice(rep) && rep.Subject != value.SourceRep.Subject)
        && value.ReceivingReps.Select(row => row.Subject).Distinct(StringComparer.Ordinal).Count() == value.ReceivingReps.Count
        && value.Assignments is not null && value.Assignments.All(row => ValidTransferScope(row, null) && row.AssignedTo is null)
        && value.Assignments.Select(row => row.Selection.AssignmentId).Distinct().Count() == value.Assignments.Count;
    private static bool ValidTransferScope(TransferScope? value, TransferScope? parent) => value is not null
        && value.Selection is { } selection && selection.AssignmentId != Guid.Empty && selection.Target is { } target
        && Enum.IsDefined(target.Level) && target.UnitId != Guid.Empty && !string.IsNullOrWhiteSpace(value.Name)
        && value.Context is not null && value.Locations >= 0 && (value.AssignedTo is null || ValidChoice(value.AssignedTo))
        && value.Children is not null && (parent is null || value.Children.Count == 0 && selection.AssignmentId == parent.Selection.AssignmentId
            && (int)target.Level == (int)parent.Selection.Target.Level + 1)
        && (target.Level is TerritoryLevel.Region or TerritoryLevel.County || value.Children.Count == 0)
        && value.Children.All(child => ValidTransferScope(child, value))
        && value.Children.Select(child => child.Selection.Target).Distinct().Count() == value.Children.Count
        && (value.Children.Count == 0 || value.Children.Sum(child => (long)child.Locations) == value.Locations);
    private static bool ValidTerritory(RepTerritoryPage value) => ValidChoice(value.Rep)
        && (value.Manager is null || ValidChoice(value.Manager)) && value.PrimaryLocations >= 0 && value.Assignments is not null
        && value.Assignments.All(row => row is not null && row.Assignment is not null && row.Assignment.Assignment is { } assignment
            && assignment.Id != Guid.Empty && assignment.RepSubject == value.Rep.Subject && !string.IsNullOrWhiteSpace(assignment.Version)
            && assignment.Target is not null && Enum.IsDefined(assignment.Target.Level) && assignment.Target.UnitId != Guid.Empty
            && !string.IsNullOrWhiteSpace(row.Assignment.Name) && row.Context is not null && row.Locations >= 0
            && ValidCarveOuts(row.CarveOuts, value.Rep.Subject, row.Locations) && row.Towns is not null
            && (assignment.Target.Level == TerritoryLevel.County || row.Towns.Count == 0)
            && row.Towns.All(town => town is not null && town.Id != Guid.Empty && !string.IsNullOrWhiteSpace(town.Name)
                && town.Locations >= 0 && (town.AssignedTo is null || ValidChoice(town.AssignedTo) && town.AssignedTo.Subject != value.Rep.Subject)
                && ValidCarveOuts(town.CarveOuts, value.Rep.Subject, town.Locations))
            && row.Towns.Select(town => town.Id).Distinct().Count() == row.Towns.Count
            && (assignment.Target.Level != TerritoryLevel.County || row.Towns.Sum(town => (long)town.Locations) == row.Locations))
        && value.Assignments.Select(row => row.Assignment.Assignment.Id).Distinct().Count() == value.Assignments.Count
        && value.PrimaryLocations <= value.Assignments.Sum(row => (long)row.Locations);
    private static bool ValidCarveOuts(IReadOnlyList<TerritoryCarveOut>? rows, string rep, int count) => rows is not null
        && rows.All(row => row is not null && ValidChoice(row.Rep) && row.Rep.Subject != rep && row.Locations > 0)
        && rows.Select(row => row.Rep.Subject).Distinct(StringComparer.Ordinal).Count() == rows.Count
        && rows.Sum(row => (long)row.Locations) <= count;
    private static bool ValidOwner(ImpactOwnerDetails? owner) => owner is null || ValidChoice(owner.Rep)
        && owner.Source is not null && Enum.IsDefined(owner.Source.Level) && owner.Source.UnitId != Guid.Empty && !string.IsNullOrWhiteSpace(owner.SourceName);
    private static bool ValidChoice(StaffChoice? value) => value is not null && !string.IsNullOrWhiteSpace(value.Subject) && !string.IsNullOrWhiteSpace(value.Name);
    private static bool ValidLine(RepReportingLineDetails? value) => value is not null && !string.IsNullOrWhiteSpace(value.RepSubject)
        && !string.IsNullOrWhiteSpace(value.ManagerSubject) && !string.IsNullOrWhiteSpace(value.Version);

    private static bool ValidLocationHistory(LocationCoverageHistoryPage page)
    {
        if (page.LocationId == Guid.Empty || string.IsNullOrWhiteSpace(page.Name) || string.IsNullOrWhiteSpace(page.TownName)
            || page.Entries is null) return false;
        long previousSequence = long.MaxValue;
        HashSet<Guid> ids = [];
        foreach (var row in page.Entries)
        {
            if (row is null || row.Id == Guid.Empty || !ids.Add(row.Id) || row.Sequence <= 0 || row.Sequence >= previousSequence
                || row.OperationId == Guid.Empty || row.LocationId != page.LocationId || string.IsNullOrWhiteSpace(row.LocationName)
                || row.ChangedAt == default || !ValidIdentity(row.Actor) || !Enum.IsDefined(row.Cause)
                || !ValidHistoryOwner(row.PreviousOwner, page.LocationId) || !ValidHistoryOwner(row.NewOwner, page.LocationId)
                || row.PreviousOwner?.Rep.Subject == row.NewOwner?.Rep.Subject || string.IsNullOrWhiteSpace(row.Display)) return false;
            previousSequence = row.Sequence;
        }
        return true;
    }
    private static bool ValidIdentity(HistoryIdentityDetails? value) => value is not null
        && !string.IsNullOrWhiteSpace(value.Subject) && !string.IsNullOrWhiteSpace(value.DisplayName);
    private static bool ValidHistoryOwner(HistoryOwnerDetails? value, Guid locationId) => value is null
        || ValidIdentity(value.Rep) && value.Source is { Target: { } target } source && source.AssignmentId != Guid.Empty
        && Enum.IsDefined(target.Level) && target.UnitId != Guid.Empty && !string.IsNullOrWhiteSpace(source.Name)
        && (target.Level != TerritoryLevel.Location || target.UnitId == locationId);
}

public sealed record AssignmentSaveResult(bool Saved, AssignmentImpactDetails? Preview = null);
