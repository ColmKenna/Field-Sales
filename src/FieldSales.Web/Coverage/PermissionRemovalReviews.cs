using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace FieldSales.Web.Coverage;

public sealed record PermissionRemovalRep(string Subject, string Name, Guid ExpectedLastChangeId);
public sealed record PermissionRemovalState(string ActorSubject, Guid GroupId, string GroupName, bool Archived,
    IReadOnlyList<PermissionRemovalRep> Reps, string? Reason, DateTimeOffset ExpiresAt);
public sealed record PermissionRemovalReview(string Proof, PermissionRemovalState State)
{
    public string Consequence => State.Archived
        ? $"{State.Reps[0].Name}'s permission for {State.GroupName} will be removed. This group is archived and already grants no access. Restoring the group will not restore the permission. Any lines already on unsent orders will still send."
        : $"{State.Reps[0].Name} will lose {State.GroupName} at their next Sync. Any lines already on unsent orders will still send.";
}
public sealed record PermissionRemovalReviewModel(PermissionRemovalReview Review, string CancelUrl);

// Website confirmation only: the business API still rechecks current authority and expected change ids.
// Use the website's persisted key ring, the same pattern as AccessChangedTokenService.
public sealed class PermissionRemovalReviews(IDataProtectionProvider keys, TimeProvider clock)
{
    private readonly IDataProtector _protector = keys.CreateProtector("FieldSales.Web.PermissionRemoval.v1");

    public PermissionRemovalReview Create(string actor, Guid groupId, string groupName, bool archived,
        IReadOnlyList<PermissionRemovalRep> reps, string? reason)
    {
        reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (reason is { Length: > 1000 } || reason?.Any(char.IsControl) == true)
            throw new ArgumentException("Enter a reason of up to 1000 characters.", nameof(reason));
        var state = new PermissionRemovalState(actor, groupId, groupName, archived, reps.ToArray(), reason, clock.GetUtcNow().AddMinutes(30));
        if (!Valid(state)) throw new ArgumentException("Choose a valid permission removal.", nameof(reps));
        return new(_protector.Protect(JsonSerializer.Serialize(state)), state);
    }

    public PermissionRemovalState? Read(string? proof, string actor)
    {
        if (string.IsNullOrWhiteSpace(proof) || proof.Length > 1024 * 1024) return null;
        try
        {
            var state = JsonSerializer.Deserialize<PermissionRemovalState>(_protector.Unprotect(proof));
            return state is not null && Valid(state) && state.ActorSubject == actor && state.ExpiresAt > clock.GetUtcNow() ? state : null;
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException or FormatException)
        { return null; }
    }

    private static bool Valid(PermissionRemovalState state) => !string.IsNullOrWhiteSpace(state.ActorSubject)
        && state.GroupId != Guid.Empty && !string.IsNullOrWhiteSpace(state.GroupName)
        && state.Reps is { Count: 1 } && state.Reps.All(rep => rep is not null && !string.IsNullOrWhiteSpace(rep.Subject)
            && !string.IsNullOrWhiteSpace(rep.Name) && rep.ExpectedLastChangeId != Guid.Empty && rep.Subject != state.ActorSubject)
        && (state.Reason is null || state.Reason.Length <= 1000 && !state.Reason.Any(char.IsControl) && state.Reason == state.Reason.Trim());
}
