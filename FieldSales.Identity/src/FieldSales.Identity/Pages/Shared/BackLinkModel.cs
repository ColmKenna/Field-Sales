namespace FieldSales.Identity.Pages.Shared;

public class BackLinkModel
{
    public string Page { get; }
    public string Text { get; }
    public IDictionary<string, string>? RouteValues { get; }

    public BackLinkModel(string page = "./Index", string text = "Back to list", IDictionary<string, string>? routeValues = null)
    {
        Page = page;
        Text = text;
        RouteValues = routeValues;
    }
}
