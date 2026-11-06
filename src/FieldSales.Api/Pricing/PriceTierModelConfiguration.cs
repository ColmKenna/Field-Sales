using FieldSales.Api.Catalogue;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Pricing;

/// <summary>Price tiers are stored in the catalogue database beside Products, so every override names a real product (WI-060 D3).
/// The tables hold terms only: no column holds a resulting price (D2).</summary>
public static class PriceTierModelConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        var tiers = modelBuilder.Entity<PriceTier>();
        tiers.ToTable("PriceTiers", table => table.HasCheckConstraint("CK_PriceTiers_PercentOff",
            "[PercentOff] > 0 AND [PercentOff] < 100 AND LEN([Name]) > 0"));
        tiers.HasKey(tier => tier.Id);
        tiers.Property(tier => tier.Id).ValueGeneratedNever();
        tiers.Property(tier => tier.Name).HasMaxLength(PriceTier.MaximumNameLength)
            .UseCollation("Latin1_General_100_CI_AS").IsRequired();
        tiers.HasIndex(tier => tier.Name).IsUnique().HasDatabaseName("UX_PriceTiers_Name");
        tiers.Property(tier => tier.PercentOff).HasPrecision(5, 2);
        tiers.HasMany(tier => tier.Overrides).WithOne().HasForeignKey(entry => entry.PriceTierId).OnDelete(DeleteBehavior.Restrict);
        tiers.Navigation(tier => tier.Overrides).HasField("_overrides").UsePropertyAccessMode(PropertyAccessMode.Field);

        var overrides = modelBuilder.Entity<PriceTierOverride>();
        overrides.ToTable("PriceTierOverrides", table => table.HasCheckConstraint("CK_PriceTierOverrides_Term",
            "([PercentOff] IS NOT NULL AND [FixedPrice] IS NULL AND [PercentOff] > 0 AND [PercentOff] < 100) OR " +
            "([PercentOff] IS NULL AND [FixedPrice] IS NOT NULL AND [FixedPrice] > 0)"));
        overrides.HasKey(entry => new { entry.PriceTierId, entry.ProductId });
        overrides.Property(entry => entry.PercentOff).HasPrecision(5, 2);
        overrides.Property(entry => entry.FixedPrice).HasPrecision(18, 2);
        overrides.Property(entry => entry.SetOn).HasColumnType("date");
        overrides.Ignore(entry => entry.Term);
        overrides.HasOne<Product>().WithMany().HasForeignKey(entry => entry.ProductId).OnDelete(DeleteBehavior.Restrict);
        overrides.HasIndex(entry => entry.ProductId);
    }
}
