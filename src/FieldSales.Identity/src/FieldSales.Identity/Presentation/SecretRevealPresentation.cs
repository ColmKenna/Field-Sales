using FieldSales.Identity.Services.SecretReveals;

namespace FieldSales.Identity.Presentation;

public static class SecretRevealPresentation
{
    // TempData carries only a handle; purpose and target are always explicit at the call site.
    public const string HandleKey = "SecretRevealHandle";

    public static async Task<string?> TryRevealAsync(this ISecretRevealService service,
        SecretRevealTarget target, string? handle, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(handle) || string.IsNullOrWhiteSpace(target.TargetId)) return null;
        var reveal = await service.ConsumeAsync(target, SecretRevealHandle.Create(handle), cancellationToken);
        return reveal.Status == SecretRevealConsumeStatus.Revealed ? reveal.Plaintext : null;
    }

    public static async Task<string> IssueHandleAsync(this ISecretRevealService service,
        SecretRevealTarget target, string plaintext, CancellationToken cancellationToken) =>
        (await service.IssueAsync(target, plaintext, cancellationToken)).Handle.Value;
}
