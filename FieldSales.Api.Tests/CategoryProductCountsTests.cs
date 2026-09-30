using FieldSales.Api.Catalogue;

namespace FieldSales.Api.Tests;

public sealed class CategoryProductCountsTests
{
    [Fact]
    public void Should_CountEachProductOnceAtEveryAncestor_When_ProductsOccupyBranchesAndDeepLeaves()
    {
        CategoryTree tree = new([]);
        List<Category> categories = [];
        Guid? parent = null;
        for (int level = 0; level < 7; level++)
        {
            Category category = tree.Add($"Level {level}", parent);
            categories.Add(category);
            parent = category.Id;
        }
        Category sibling = tree.Add("Sibling", categories[1].Id);
        Category empty = tree.Add("Empty", categories[0].Id);
        Category otherRoot = tree.Add("Other root");
        categories.AddRange([sibling, empty, otherRoot]);
        Dictionary<Guid, int> direct = new()
        {
            [categories[0].Id] = 3, [categories[2].Id] = 12,
            [categories[6].Id] = 64, [sibling.Id] = 48, [otherRoot.Id] = 500
        };

        var counts = CategoryProductCounts.Calculate(categories, direct);

        Assert.Equal(new CategoryProductCount(3, 127), counts[categories[0].Id]);
        Assert.Equal(new CategoryProductCount(0, 124), counts[categories[1].Id]);
        Assert.Equal(new CategoryProductCount(12, 76), counts[categories[2].Id]);
        Assert.Equal(new CategoryProductCount(64, 64), counts[categories[6].Id]);
        Assert.Equal(new CategoryProductCount(48, 48), counts[sibling.Id]);
        Assert.Equal(new CategoryProductCount(0, 0), counts[empty.Id]);
        Assert.Equal(new CategoryProductCount(500, 500), counts[otherRoot.Id]);
    }

    [Fact]
    public void Should_HandleDepthWithoutRecursiveCalls_When_CategoryChainIsLong()
    {
        CategoryTree tree = new([]);
        List<Category> categories = [];
        Guid? parent = null;
        for (int level = 0; level < 2000; level++)
        {
            Category category = tree.Add($"Level {level}", parent);
            categories.Add(category);
            parent = category.Id;
        }
        var counts = CategoryProductCounts.Calculate(categories, new Dictionary<Guid, int> { [parent!.Value] = 1 });
        Assert.All(counts.Values, count => Assert.Equal(1, count.Beneath));
        Assert.Equal(0, counts[categories[0].Id].Here);
        Assert.Equal(1, counts[categories[^1].Id].Here);
    }

    [Fact]
    public void Should_RejectIncompleteHierarchy_When_ParentIsMissingFromRead()
    {
        CategoryTree tree = new([]);
        Category root = tree.Add("Health");
        Category child = tree.Add("Suncare", root.Id);
        Assert.Throws<InvalidOperationException>(() => CategoryProductCounts.Calculate([child], new Dictionary<Guid, int>()));
    }
}
