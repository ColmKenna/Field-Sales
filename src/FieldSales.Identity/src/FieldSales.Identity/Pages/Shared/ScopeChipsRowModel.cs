namespace FieldSales.Identity.Pages.Shared;

public class ScopeChipsRowModel
{
    public IReadOnlyList<ScopeChipItem> Items { get; }
    public string EmptyStateText { get; }
    public string HandlerName { get; }
    public string RouteNameValue { get; }
    public string RouteParamKey { get; }
    public string RouteNameKey { get; }

    public ScopeChipsRowModel(
        IReadOnlyList<ScopeChipItem> items,
        string emptyStateText,
        string handlerName,
        string routeNameValue,
        string routeParamKey = "claimType",
        string routeNameKey = "name")
    {
        Items = items;
        EmptyStateText = emptyStateText;
        HandlerName = handlerName;
        RouteNameValue = routeNameValue;
        RouteParamKey = routeParamKey;
        RouteNameKey = routeNameKey;
    }

    public static ScopeChipsRowModel ForSimpleList(
        IEnumerable<string> items,
        string emptyStateText,
        string handlerName,
        string routeNameValue,
        string routeParamKey = "claimType",
        string routeNameKey = "name")
    {
        return new ScopeChipsRowModel(
            items.Select(i => new ScopeChipItem(i)).ToList(),
            emptyStateText,
            handlerName,
            routeNameValue,
            routeParamKey,
            routeNameKey
        );
    }
}
