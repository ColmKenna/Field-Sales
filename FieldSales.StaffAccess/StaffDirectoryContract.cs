using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;

namespace FieldSales.StaffAccess;

public sealed record StaffDirectoryEntry(string Subject, string DisplayName, string[] Roles, bool Available);
public sealed record StaffDirectoryLookupRequest(string[]? Subjects);

public static class StaffDirectoryContract
{
    public const int MaximumSubjects = 2048;
    public const int MaximumDisplayNameLength = 512;
    public static bool ValidSubject(string? subject) => !string.IsNullOrWhiteSpace(subject)
        && subject.Length <= 450 && subject == subject.Trim() && !subject.Any(char.IsControl);

    public static IReadOnlyDictionary<string, StaffDirectoryEntry> Validate(IEnumerable<StaffDirectoryEntry> entries)
    {
        Dictionary<string, StaffDirectoryEntry> result = new(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (entry is null || !ValidSubject(entry.Subject) || string.IsNullOrWhiteSpace(entry.DisplayName)
                || entry.DisplayName.Length > MaximumDisplayNameLength || entry.DisplayName.Any(char.IsControl)
                || entry.Roles is null || entry.Roles.Any(role => !BusinessRoles.Contains(role))
                || !result.TryAdd(entry.Subject, entry))
                throw new InvalidDataException("The staff directory response is invalid.");
        }
        return result;
    }
}

public interface IStaffDirectory
{
    Task<IReadOnlyList<StaffDirectoryEntry>> ListAsync(string accessToken, CancellationToken ct);
    Task<IReadOnlyList<StaffDirectoryEntry>> LookupAsync(string accessToken, IReadOnlyCollection<string> subjects, CancellationToken ct);
}

public sealed class HttpStaffDirectory(HttpClient client, IConfiguration configuration) : IStaffDirectory
{
    public Task<IReadOnlyList<StaffDirectoryEntry>> ListAsync(string accessToken, CancellationToken ct) =>
        SendAsync(accessToken, null, ct);

    public Task<IReadOnlyList<StaffDirectoryEntry>> LookupAsync(string accessToken, IReadOnlyCollection<string> subjects, CancellationToken ct)
    {
        if (subjects.Count is 0 or > StaffDirectoryContract.MaximumSubjects || subjects.Any(subject => !StaffDirectoryContract.ValidSubject(subject)))
            throw new ArgumentException("Choose valid staff subjects.", nameof(subjects));
        return SendAsync(accessToken, subjects.Distinct(StringComparer.Ordinal).ToArray(), ct);
    }

    private async Task<IReadOnlyList<StaffDirectoryEntry>> SendAsync(string accessToken, string[]? subjects, CancellationToken ct)
    {
        string authority = configuration["Authentication:Authority"]
            ?? throw new InvalidOperationException("Authentication:Authority is required.");
        using HttpRequestMessage request = new(subjects is null ? HttpMethod.Get : HttpMethod.Post,
            authority.TrimEnd('/') + "/staff/directory" + (subjects is null ? "" : "/lookup"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (subjects is not null) request.Content = JsonContent.Create(new StaffDirectoryLookupRequest(subjects));
        using var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var entries = await response.Content.ReadFromJsonAsync<StaffDirectoryEntry[]>(ct)
            ?? throw new InvalidDataException("The staff directory response is missing.");
        StaffDirectoryContract.Validate(entries);
        if (subjects is not null && entries.Any(entry => !subjects.Contains(entry.Subject, StringComparer.Ordinal)))
            throw new InvalidDataException("The staff directory returned an unrequested subject.");
        return entries;
    }
}
