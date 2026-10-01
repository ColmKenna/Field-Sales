using Microsoft.EntityFrameworkCore;
using FieldSales.Quantities;

namespace FieldSales.Api.Catalogue;

public sealed class CatalogueDbContext(DbContextOptions<CatalogueDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductBasePrice> ProductBasePrices => Set<ProductBasePrice>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<ProductAlternativeBrand> ProductAlternativeBrands => Set<ProductAlternativeBrand>();
    public DbSet<ProductProfile> ProductProfiles => Set<ProductProfile>();
    public DbSet<AttributeName> AttributeNames => Set<AttributeName>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<ProductAttributeValue> ProductAttributeValues => Set<ProductAttributeValue>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Ignore<NamedReferenceItem>();
        ConfigureReference<Brand>(modelBuilder, "Brands");
        ConfigureReference<ProductProfile>(modelBuilder, "ProductProfiles");
        ConfigureReference<AttributeName>(modelBuilder, "AttributeNames");
        ConfigureReference<Supplier>(modelBuilder, "Suppliers");
        modelBuilder.Entity<ProductAttributeValue>(entity =>
        {
            entity.ToTable("ProductAttributeValues");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Id).ValueGeneratedNever();
            entity.Property(value => value.Value).IsRequired();
            entity.HasIndex(value => new { value.ProductId, value.Position }).IsUnique();
            entity.HasOne(value => value.AttributeName).WithMany().HasForeignKey(value => value.AttributeNameId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ProductAlternativeBrand>(entity =>
        {
            entity.ToTable("ProductAlternativeBrands");
            entity.HasKey(link => new { link.ProductId, link.BrandId });
            entity.HasOne<Brand>().WithMany().HasForeignKey(link => link.BrandId).OnDelete(DeleteBehavior.Restrict);
        });
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
            entity.ToTable("Products", table => table.HasCheckConstraint("CK_Products_QuantityRules",
                "([Unit] = 'Each' AND [QuantityStep] IS NULL AND [MinimumQuantity] IS NULL) OR " +
                "([Unit] IN ('kg', 'litre', 'metre') AND [QuantityStep] IS NOT NULL AND [MinimumQuantity] IS NOT NULL " +
                "AND [QuantityStep] > 0 AND [MinimumQuantity] > 0 AND [MinimumQuantity] % NULLIF([QuantityStep], 0) = 0)"));
            entity.HasKey(product => product.Id);
            entity.Property(product => product.Code).HasMaxLength(Product.MaximumCodeLength)
                .UseCollation("Latin1_General_100_CI_AS").IsRequired();
            entity.HasIndex(product => product.Code).IsUnique().HasDatabaseName("UX_Products_Code");
            entity.Property(product => product.Name).HasMaxLength(Product.MaximumNameLength).IsRequired();
            entity.Property(product => product.Unit).HasMaxLength(20).IsRequired();
            entity.Property(product => product.QuantityStep).HasPrecision(Quantity.Precision, Quantity.Scale);
            entity.Property(product => product.MinimumQuantity).HasPrecision(Quantity.Precision, Quantity.Scale);
            entity.HasOne<Category>().WithMany().HasForeignKey(product => product.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Brand>().WithMany().HasForeignKey(product => product.PrimaryBrandId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProductProfile>().WithMany().HasForeignKey(product => product.ProductProfileId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Supplier>().WithMany().HasForeignKey(product => product.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(product => product.AttributeValues).WithOne().HasForeignKey(value => value.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.Navigation(product => product.AttributeValues).HasField("_attributeValues")
                .UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.HasMany(product => product.AlternativeBrands).WithOne().HasForeignKey(link => link.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.Navigation(product => product.AlternativeBrands).HasField("_alternativeBrands")
                .UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.HasOne<Product>().WithMany().HasForeignKey(product => product.ParentProductId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.Ignore(product => product.Attributes);
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

    private static void ConfigureReference<T>(ModelBuilder modelBuilder, string table) where T : NamedReferenceItem
    {
        var entity = modelBuilder.Entity<T>();
        entity.HasBaseType((Type?)null);
        entity.ToTable(table);
        entity.HasKey(item => item.Id);
        entity.Property(item => item.Name).HasMaxLength(NamedReferenceItem.MaximumNameLength)
            .UseCollation("Latin1_General_100_CI_AS").IsRequired();
        entity.HasIndex(item => item.Name).IsUnique().HasDatabaseName($"UX_{table}_Name");
        entity.Property(item => item.Version).IsRowVersion();
    }
}
