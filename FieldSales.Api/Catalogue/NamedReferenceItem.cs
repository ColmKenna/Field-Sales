namespace FieldSales.Api.Catalogue;

public abstract class NamedReferenceItem
{
    public const int MaximumNameLength = 200;
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsArchived { get; private set; }
    public byte[] Version { get; private set; } = [];
    protected abstract string ItemLabel { get; }
    protected void Initialize(string name)
    {
        Id = Guid.NewGuid();
        Rename(name);
    }
    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > MaximumNameLength)
            throw new ArgumentException($"Enter a {ItemLabel} name of up to 200 characters.", nameof(name));
        Name = name.Trim();
    }
    public void Archive() => IsArchived = true;
    public void Unarchive() => IsArchived = false;
}

public sealed class ProductProfile : NamedReferenceItem
{
    private ProductProfile() { }
    protected override string ItemLabel => "product profile";
    public static ProductProfile Create(string name)
    {
        ProductProfile item = new();
        item.Initialize(name);
        return item;
    }
}

public sealed class AttributeName : NamedReferenceItem
{
    private AttributeName() { }
    protected override string ItemLabel => "attribute";
    public static AttributeName Create(string name)
    {
        AttributeName item = new();
        item.Initialize(name);
        return item;
    }
}

public sealed class Supplier : NamedReferenceItem
{
    private Supplier() { }
    protected override string ItemLabel => "supplier";
    public static Supplier Create(string name)
    {
        Supplier item = new();
        item.Initialize(name);
        return item;
    }
}

public sealed class ProductAttributeValue
{
    private ProductAttributeValue() { }
    internal ProductAttributeValue(Guid productId, AttributeName name, string value, int position)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (name.IsArchived) throw new ArgumentException("Archived attribute names cannot be selected for new references.", nameof(name));
        Id = Guid.NewGuid();
        ProductId = productId;
        AttributeNameId = name.Id;
        AttributeName = name;
        Value = value;
        Position = position;
    }
    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid AttributeNameId { get; private set; }
    public AttributeName AttributeName { get; private set; } = null!;
    public string Value { get; private set; } = string.Empty;
    public int Position { get; private set; }
}
