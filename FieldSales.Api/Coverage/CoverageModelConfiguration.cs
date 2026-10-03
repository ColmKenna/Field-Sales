using FieldSales.Api.Directory;
using FieldSales.Directory.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Coverage;

public static class CoverageModelConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        var reporting = modelBuilder.Entity<RepReportingLine>();
        reporting.ToTable("RepReportingLines", table =>
        {
            table.HasCheckConstraint("CK_RepReportingLines_Subjects", "LEN([RepSubject]) > 0 AND LEN([ManagerSubject]) > 0");
        });
        reporting.HasKey(item => item.RepSubject);
        reporting.Property(item => item.RepSubject).HasMaxLength(CoverageFields.MaximumSubjectLength)
            .UseCollation("Latin1_General_100_BIN2").ValueGeneratedNever();
        reporting.Property(item => item.ManagerSubject).HasMaxLength(CoverageFields.MaximumSubjectLength)
            .UseCollation("Latin1_General_100_BIN2").IsRequired();
        reporting.Property(item => item.Version).IsRowVersion();
        reporting.HasIndex(item => item.ManagerSubject);

        var assignments = modelBuilder.Entity<TerritoryAssignment>();
        assignments.ToTable("TerritoryAssignments", table => table.HasCheckConstraint("CK_TerritoryAssignments_OneTarget", """
            (CASE WHEN [RegionId] IS NULL THEN 0 ELSE 1 END
             + CASE WHEN [CountyId] IS NULL THEN 0 ELSE 1 END
             + CASE WHEN [TownId] IS NULL THEN 0 ELSE 1 END
             + CASE WHEN [LocationId] IS NULL THEN 0 ELSE 1 END) = 1
            AND ([RegionId] IS NULL OR [RegionId] <> '00000000-0000-0000-0000-000000000000')
            AND ([CountyId] IS NULL OR [CountyId] <> '00000000-0000-0000-0000-000000000000')
            AND ([TownId] IS NULL OR [TownId] <> '00000000-0000-0000-0000-000000000000')
            AND ([LocationId] IS NULL OR [LocationId] <> '00000000-0000-0000-0000-000000000000')
            """.ReplaceLineEndings("\n")));
        assignments.HasKey(item => item.Id);
        assignments.Property(item => item.Id).ValueGeneratedNever();
        assignments.Property(item => item.RepSubject).HasMaxLength(CoverageFields.MaximumSubjectLength)
            .UseCollation("Latin1_General_100_BIN2").IsRequired();
        assignments.Property(item => item.Version).IsRowVersion();
        assignments.Ignore(item => item.Target);
        assignments.HasIndex(item => item.RepSubject);
        assignments.HasIndex(item => item.RegionId).IsUnique().HasFilter("[RegionId] IS NOT NULL");
        assignments.HasIndex(item => item.CountyId).IsUnique().HasFilter("[CountyId] IS NOT NULL");
        assignments.HasIndex(item => item.TownId).IsUnique().HasFilter("[TownId] IS NOT NULL");
        assignments.HasIndex(item => item.LocationId).IsUnique().HasFilter("[LocationId] IS NOT NULL");
        assignments.HasOne<RepReportingLine>().WithMany().HasForeignKey(item => item.RepSubject).OnDelete(DeleteBehavior.Restrict);
        assignments.HasOne<Region>().WithMany().HasForeignKey(item => item.RegionId).OnDelete(DeleteBehavior.Restrict);
        assignments.HasOne<County>().WithMany().HasForeignKey(item => item.CountyId).OnDelete(DeleteBehavior.Restrict);
        assignments.HasOne<Town>().WithMany().HasForeignKey(item => item.TownId).OnDelete(DeleteBehavior.Restrict);
        assignments.HasOne<Location>().WithMany().HasForeignKey(item => item.LocationId).OnDelete(DeleteBehavior.Restrict);
    }
}
