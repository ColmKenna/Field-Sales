namespace FieldSales.Identity.Pages.Shared;

public class SecretRevealBannerModel
{
    public string? SecretValue { get; }
    public string? ClientId { get; }

    /// <summary>Literal markup. Use <c>{0}</c> placeholders for runtime values; see <see cref="DescriptionArgs" />.</summary>
    public string? Description { get; }

    /// <summary>Runtime values substituted into <see cref="Description" />, HTML-encoded on render.</summary>
    public IReadOnlyList<string> DescriptionArgs { get; }

    public SecretRevealBannerModel(
        string? secretValue,
        string? clientId = null,
        string? description = null,
        IReadOnlyList<string>? descriptionArgs = null)
    {
        SecretValue = secretValue;
        ClientId = clientId;
        Description = description;
        DescriptionArgs = descriptionArgs ?? Array.Empty<string>();
    }
}
