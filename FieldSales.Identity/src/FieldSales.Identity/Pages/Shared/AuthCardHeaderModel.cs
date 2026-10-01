namespace FieldSales.Identity.Pages.Shared;

public class AuthCardHeaderModel
{
    public string BrandSubtitle { get; }
    public string Title { get; }
    public string? Description { get; }

    public AuthCardHeaderModel(string brandSubtitle, string title, string? description = null)
    {
        BrandSubtitle = brandSubtitle;
        Title = title;
        Description = description;
    }
}
