using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using FieldSales.Directory.Contracts;
using FieldSales.StaffAccess;
using Microsoft.AspNetCore.DataProtection;

namespace FieldSales.Web.Coverage;

// Protect only navigation intent. Authority and impact proof still belong to the API.
public sealed record LocationChangeContext(Guid LocationId, Guid TownId, Guid? SourceAssignmentId,
    TerritoryTarget? Source, string? OwnerSubject, string Intent, string ActorSubject)
{
    private const string Purpose = "FieldSales.Coverage.LocationEntry.v1";
    public static string Issue(IDataProtectionProvider protection, ClaimsPrincipal actor, LocationCoverageActions actions, string intent)
    {
        var page = actions.Location;
        return protection.CreateProtector(Purpose).Protect(JsonSerializer.Serialize(new LocationChangeContext(
            page.LocationId, page.TownId, actions.SourceAssignmentId, page.Owner?.Source, page.Owner?.Rep.Subject,
            intent, actor.GetStaffSubject() ?? "")));
    }
    public static LocationChangeContext? Read(IDataProtectionProvider protection, ClaimsPrincipal actor, string? proof)
    {
        if (string.IsNullOrWhiteSpace(proof) || proof.Length > 16000) return null;
        try
        {
            var entry = JsonSerializer.Deserialize<LocationChangeContext>(protection.CreateProtector(Purpose).Unprotect(proof));
            return entry is not null && entry.LocationId != Guid.Empty && entry.TownId != Guid.Empty
                && entry.Intent is "Town" or "Shop" or "Source" && !string.IsNullOrWhiteSpace(entry.ActorSubject)
                && entry.ActorSubject == actor.GetStaffSubject() ? entry : null;
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException or ArgumentException) { return null; }
    }
    public bool Matches(LocationCoverageActions actions, bool saving = false)
    {
        var page = actions.Location;
        if (LocationId != page.LocationId || TownId != page.TownId || !Available(actions)) return false;
        // Same-scope Add saves use the existing API's fresh preview/reconfirmation.
        if (saving && Intent == "Shop" && Source?.Level != TerritoryLevel.Location
            && page.Owner?.Source.Level != TerritoryLevel.Location) return true;
        return SourceAssignmentId == actions.SourceAssignmentId && Source == page.Owner?.Source && OwnerSubject == page.Owner?.Rep.Subject;
    }
    public bool Available(LocationCoverageActions actions) => Intent switch
    {
        "Town" => actions.CanAssignTown,
        "Shop" => actions.CanChangeShop,
        "Source" => actions.CanTransferSource,
        _ => false
    };
    public bool IsTransfer => Intent == "Source" || Intent == "Shop" && Source?.Level == TerritoryLevel.Location;
    public TerritoryTarget Target => Intent == "Town" ? new(TerritoryLevel.Town, TownId)
        : Intent == "Shop" ? new(TerritoryLevel.Location, LocationId) : Source!;
}
