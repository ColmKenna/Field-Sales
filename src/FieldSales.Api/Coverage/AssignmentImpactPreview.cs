using System.Security.Cryptography;
using System.Text.Json;
using FieldSales.Api.Directory;
using FieldSales.Directory.Contracts;
using FieldSales.StaffAccess;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Coverage;

public sealed record AssignmentOwnershipChange(LocationOwnershipPath Location, EffectiveOwner? Previous, EffectiveOwner? Next);

// A dry run of the same resolver used for reads and writes, with no I/O or mutation.
public static class AssignmentImpactPreview
{
    public static IReadOnlyList<AssignmentOwnershipChange> Calculate(IEnumerable<LocationOwnershipPath> locations,
        IEnumerable<TerritoryAssignment> before, IEnumerable<TerritoryAssignment> proposed)
    {
        var oldOwners = new EffectiveOwnerResolver(before);
        var newOwners = new EffectiveOwnerResolver(proposed);
        return locations.Select(path => new AssignmentOwnershipChange(path, oldOwners.Resolve(path), newOwners.Resolve(path)))
            .Where(change => !string.Equals(change.Previous?.RepSubject, change.Next?.RepSubject, StringComparison.Ordinal))
            .OrderBy(change => change.Location.LocationName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(change => change.Location.LocationId).ToArray();
    }
}

internal sealed record AssignmentImpactInputs(LocationOwnershipPath[] Locations, TerritoryAssignment[] Assignments,
    object Geography, object ReportingLines)
{
    public static async Task<AssignmentImpactInputs> ReadAsync(DirectoryDbContext db, CoverageOwnershipReader ownership, CancellationToken ct)
    {
        // Conservative invalidation includes geography and reporting rows even when
        // an unrelated save changed them. A false refresh is safe; stale application isn't.
        var regions = await db.Regions.AsNoTracking().OrderBy(row => row.Id)
            .Select(row => new { row.Id, row.Name, row.IsArchived, row.Version }).ToArrayAsync(ct);
        var counties = await db.Counties.AsNoTracking().OrderBy(row => row.Id)
            .Select(row => new { row.Id, row.Name, row.RegionId, row.IsArchived, row.Version }).ToArrayAsync(ct);
        var towns = await db.Towns.AsNoTracking().OrderBy(row => row.Id)
            .Select(row => new { row.Id, row.Name, row.CountyId, row.IsArchived, row.Version }).ToArrayAsync(ct);
        var lines = await db.RepReportingLines.AsNoTracking().OrderBy(row => row.RepSubject)
            .Select(row => new { row.RepSubject, row.ManagerSubject, row.Version }).ToArrayAsync(ct);
        var paths = await ownership.Paths().ToArrayAsync(ct);
        return new(paths.OrderBy(row => row.LocationId).ToArray(),
            await db.TerritoryAssignments.AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(ct),
            new { regions, counties, towns }, lines);
    }

    public OwnershipSnapshot Owners()
    {
        var resolver = new EffectiveOwnerResolver(Assignments);
        return new(Locations, Locations.ToDictionary(path => path.LocationId, resolver.Resolve));
    }

    public string Fingerprint(IReadOnlyDictionary<string, StaffDirectoryEntry> staff, string actingRole) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            Locations,
            Assignments = Assignments.Select(row => new { row.Id, row.RepSubject, row.Target, row.Version }),
            Geography, ReportingLines, actingRole,
            Staff = staff.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => new
            { pair.Key, pair.Value.DisplayName, pair.Value.Available, Roles = pair.Value.Roles.Order(StringComparer.Ordinal).ToArray() })
        })));
}

// Proofs are deliberately process-local. Restart/another instance requires a fresh
// preview, never a guessed or stale confirmation. No new schema or persisted secret.
public sealed class AssignmentPreviewProofs(TimeProvider clock)
{
    private readonly IDataProtector protector = new EphemeralDataProtectionProvider().CreateProtector("FieldSales.AssignmentPreview.v1");
    public string Issue(string actor, string command, string fingerprint) => protector.Protect(JsonSerializer.Serialize(
        new Proof(actor, command, fingerprint, clock.GetUtcNow().AddMinutes(30))));

    public bool Matches(string? value, bool confirmed, string actor, string command, string fingerprint)
    {
        if (!confirmed || string.IsNullOrEmpty(value) || value.Length > 16384)
            throw new CoverageValidationException("PreviewProof", "Review the impact and explicitly confirm this assignment change.");
        Proof? proof;
        try { proof = JsonSerializer.Deserialize<Proof>(protector.Unprotect(value)); }
        catch (Exception exception) when (exception is CryptographicException or JsonException or FormatException)
        { throw new CoverageValidationException("PreviewProof", "This preview is invalid. Review the impact again."); }
        if (proof is null || proof.Actor != actor || proof.Command != command)
            throw new CoverageValidationException("PreviewProof", "This preview does not confirm this assignment change. Review it again.");
        return proof.ExpiresAt > clock.GetUtcNow() && proof.Fingerprint == fingerprint;
    }
    private sealed record Proof(string Actor, string Command, string Fingerprint, DateTimeOffset ExpiresAt);
}

public sealed class CoveragePreviewChangedException(AssignmentImpactDetails preview) : Exception("The data changed. Review the refreshed impact before saving.")
{
    public AssignmentImpactDetails Preview { get; } = preview;
}
