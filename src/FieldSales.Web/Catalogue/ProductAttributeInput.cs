using System.ComponentModel.DataAnnotations;

namespace FieldSales.Web.Catalogue;

public sealed class ProductAttributeInput
{
    public Guid? NameId { get; set; }
    // Attribute values keep the predecessor's exact text, including empty strings and whitespace.
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? Value { get; set; } = "";
}
