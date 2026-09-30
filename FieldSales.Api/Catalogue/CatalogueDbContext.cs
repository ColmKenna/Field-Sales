using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace FieldSales.Api.Catalogue;

public sealed class CatalogueDbContext(DbContextOptions<CatalogueDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductBasePrice> ProductBasePrices => Set<ProductBasePrice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("Categories");
            entity.HasKey(category => category.Id);
            entity.Property(category => category.Name)
                .HasMaxLength(Category.MaximumNameLength)
                .UseCollation("Latin1_General_100_CI_AS")
                .IsRequired();
            entity.HasOne<Category>().WithMany().HasForeignKey(category => category.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(category => category.Name)
                .IsUnique().HasDatabaseName("UX_Categories_RootName")
                .HasFilter("[ParentId] IS NULL");
            entity.HasIndex(category => new { category.ParentId, category.Name })
                .IsUnique().HasDatabaseName("UX_Categories_SiblingName")
                .HasFilter("[ParentId] IS NOT NULL");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("Products");
            entity.HasKey(product => product.Id);
            entity.Property(product => product.Code).HasMaxLength(Product.MaximumCodeLength)
                .UseCollation("Latin1_General_100_CI_AS").IsRequired();
            entity.HasIndex(product => product.Code).IsUnique().HasDatabaseName("UX_Products_Code");
            entity.Property(product => product.Name).HasMaxLength(Product.MaximumNameLength).IsRequired();
            entity.Property(product => product.Unit).HasMaxLength(20).IsRequired();
            entity.HasOne<Category>().WithMany().HasForeignKey(product => product.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Product>().WithMany().HasForeignKey(product => product.ParentProductId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.Property(product => product.Attributes)
                .HasConversion(
                    attributes => JsonSerializer.Serialize(attributes, (JsonSerializerOptions?)null),
                    json => JsonSerializer.Deserialize<List<ProductAttribute>>(json, (JsonSerializerOptions?)null)!)
                .Metadata.SetValueComparer(new ValueComparer<IReadOnlyList<ProductAttribute>>(
                    (left, right) => left != null && right != null && left.SequenceEqual(right),
                    attributes => attributes.Aggregate(0, (hash, attribute) => HashCode.Combine(hash, attribute)),
                    attributes => attributes.ToArray()));
            entity.HasMany(product => product.BasePrices).WithOne()
                .HasForeignKey(price => price.ProductId).OnDelete(DeleteBehavior.Restrict);
            entity.Navigation(product => product.BasePrices).HasField("_basePrices")
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<ProductBasePrice>(entity =>
        {
            entity.ToTable("ProductBasePrices", table => table.HasCheckConstraint(
                "CK_ProductBasePrices_NonNegativeAmount", "[Amount] >= 0"));
            entity.HasKey(price => new { price.ProductId, price.EffectiveFrom });
            entity.Property(price => price.Amount).HasPrecision(18, 2);
            entity.Property(price => price.EffectiveFrom).HasColumnType("date");
        });
    }
}
