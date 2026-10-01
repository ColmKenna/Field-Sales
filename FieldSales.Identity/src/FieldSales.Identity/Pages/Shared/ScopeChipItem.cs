namespace FieldSales.Identity.Pages.Shared;

public class ScopeChipItem
{
    public string Text { get; }
    public bool IsLocked { get; }
    public string? LockReason { get; }

    public ScopeChipItem(string text, bool isLocked = false, string? lockReason = null)
    {
        Text = text;
        IsLocked = isLocked;
        LockReason = lockReason;
    }
}
