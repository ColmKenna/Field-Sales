using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Directory;

public static class LocationPositionModelConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        var locations = modelBuilder.Entity<Location>();
        locations.Property(item => item.Latitude).HasPrecision(10, 7);
        locations.Property(item => item.Longitude).HasPrecision(10, 7);
        locations.Property(item => item.PositionSourceEircode).HasMaxLength(Location.MaximumEircodeLength);
        locations.ToTable("Locations", table => table.HasCheckConstraint("CK_Locations_Position", """
            ([Latitude] IS NULL AND [Longitude] IS NULL AND [PositionPrecision] IS NULL
              AND [PositionSourceTownId] IS NULL AND [PositionSourceEircode] IS NULL AND [PositionedAt] IS NULL)
            OR
            ([Latitude] IS NOT NULL AND [Longitude] IS NOT NULL AND [PositionPrecision] IS NOT NULL
              AND [Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180
              AND [PositionedAt] IS NOT NULL AND
              (([PositionPrecision] = 0 AND [PositionSourceTownId] IS NOT NULL AND [PositionSourceEircode] IS NULL)
               OR ([PositionPrecision] = 1 AND [PositionSourceTownId] IS NOT NULL
                   AND [PositionSourceEircode] IS NOT NULL AND LEN([PositionSourceEircode]) > 0)
               OR ([PositionPrecision] = 2 AND [PositionSourceTownId] IS NULL AND [PositionSourceEircode] IS NULL)))
            """.ReplaceLineEndings("\n")));
        locations.HasMany(item => item.PositionHistory).WithOne().HasForeignKey(item => item.LocationId)
            .OnDelete(DeleteBehavior.Restrict);
        locations.Navigation(item => item.PositionHistory).HasField("_positionHistory")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        var history = modelBuilder.Entity<LocationPositionHistory>();
        history.ToTable("LocationPositionHistory", table => table.HasCheckConstraint("CK_LocationPositionHistory_Position", """
            [Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180 AND
            (([Precision] = 0 AND [SourceTownId] IS NOT NULL AND [SourceEircode] IS NULL)
             OR ([Precision] = 1 AND [SourceTownId] IS NOT NULL AND [SourceEircode] IS NOT NULL AND LEN([SourceEircode]) > 0)
             OR ([Precision] = 2 AND [SourceTownId] IS NULL AND [SourceEircode] IS NULL))
            """.ReplaceLineEndings("\n")));
        history.HasKey(item => item.Id);
        history.Property(item => item.Id).ValueGeneratedNever();
        history.Property(item => item.Latitude).HasPrecision(10, 7);
        history.Property(item => item.Longitude).HasPrecision(10, 7);
        history.Property(item => item.SourceEircode).HasMaxLength(Location.MaximumEircodeLength);
        history.HasIndex(item => new { item.LocationId, item.ReplacedAt });
    }
}
