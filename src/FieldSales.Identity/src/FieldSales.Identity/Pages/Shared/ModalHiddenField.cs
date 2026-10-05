namespace FieldSales.Identity.Pages.Shared;

/// <summary>
///     One hidden field carried by a confirmation dialog's form. The id is optional and only
///     needed when script fills the value before the dialog opens.
/// </summary>
public sealed record ModalHiddenField(string Name, string? Id = null, string? Value = null);
