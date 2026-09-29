using FieldSales.Api.Catalogue;

namespace FieldSales.Api.Tests;

public sealed class CategoryTreeTests
{
    [Fact]
    public void Should_ShowFullBreadcrumb_When_SixthLevelIsAdded()
    {
        CategoryTree tree = new([]);
        string[] names = ["Health", "Skincare", "Suncare", "Lotions", "Face", "Daily"];
        Guid? parentId = null;
        foreach (string name in names)
        {
            Category category = tree.Add(name, parentId);
            Assert.Equal(parentId, category.ParentId);
            parentId = category.Id;
        }

        Assert.Equal(names, tree.Breadcrumb(parentId!.Value).Select(segment => segment.Name));

        // Six is a design target, not a hard limit.
        Category seventh = tree.Add("Travel", parentId);
        Assert.Equal([.. names, "Travel"],
            tree.Breadcrumb(seventh.Id).Select(segment => segment.Name));
    }

    [Fact]
    public void Should_DistinguishSameNamedLeaves_When_ParentsDiffer()
    {
        CategoryTree tree = new([]);
        Category health = tree.Add("Health");
        Category suncare = tree.Add("Suncare", health.Id);
        Category bodyCare = tree.Add("Body Care", health.Id);
        Category sunLotions = tree.Add("Lotions", suncare.Id);
        Category bodyLotions = tree.Add("Lotions", bodyCare.Id);

        Assert.NotEqual(sunLotions.Id, bodyLotions.Id);
        Assert.Equal("Health > Suncare > Lotions",
            string.Join(" > ", tree.Breadcrumb(sunLotions.Id).Select(segment => segment.Name)));
        Assert.Equal("Health > Body Care > Lotions",
            string.Join(" > ", tree.Breadcrumb(bodyLotions.Id).Select(segment => segment.Name)));
    }

    [Fact]
    public void Should_RejectDuplicateSiblingName_When_CategoryIsAdded()
    {
        CategoryTree tree = new([]);
        Category suncare = tree.Add("Suncare");
        tree.Add("Lotions", suncare.Id);

        Assert.Throws<InvalidOperationException>(() => tree.Add(" lotions ", suncare.Id));
    }
}
