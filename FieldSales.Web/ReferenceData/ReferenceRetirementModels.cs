using FieldSales.ReferenceData;

namespace FieldSales.Web.ReferenceData;

public sealed record ReferenceRetirementModel(ReferenceListItem Item, string ConfirmationUrl);

public sealed record ReferenceConfirmationModel(ReferenceListItem Item, ReferenceListDefinition Definition,
    string ConfirmUrl, string CancelUrl, IReadOnlyDictionary<string, string> Fields,
    string UnarchiveExplanation = "It will be offered again for new selections.")
{
    public ReferenceAction Action => ReferenceRetirementPolicy.Decide(Item.IsArchived, Item.Usage);
}
