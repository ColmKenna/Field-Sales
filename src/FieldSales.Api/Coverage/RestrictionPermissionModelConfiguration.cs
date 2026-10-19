using FieldSales.Api.Catalogue;
using FieldSales.Directory.Contracts;
using FieldSales.StaffAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FieldSales.Api.Coverage;

public static class RestrictionPermissionModelConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        var current = modelBuilder.Entity<RestrictionPermission>();
        current.ToTable("RestrictionPermissions", table => table.HasCheckConstraint("CK_RestrictionPermissions_Grant",
            "LEN([RepSubject]) > 0 AND [RestrictionGroupId] <> '00000000-0000-0000-0000-000000000000'"));
        current.HasKey(row => new { row.RepSubject, row.RestrictionGroupId });
        Subject(current.Property(row => row.RepSubject));
        // A held permission keeps its group from being deleted; archive it instead.
        current.HasOne<RestrictionGroup>().WithMany().HasForeignKey(row => row.RestrictionGroupId).OnDelete(DeleteBehavior.Restrict);

        var changes = modelBuilder.Entity<RestrictionPermissionChange>();
        changes.ToTable("RestrictionPermissionChanges", table =>
        {
            table.HasTrigger("TR_RestrictionPermissionChanges_AppendOnly");
            table.UseSqlOutputClause(false);
            table.HasCheckConstraint("CK_RestrictionPermissionChanges_Change", """
                [Id] <> '00000000-0000-0000-0000-000000000000'
                AND [RestrictionGroupId] <> '00000000-0000-0000-0000-000000000000'
                AND LEN([RepSubject]) > 0 AND LEN([RepName]) > 0 AND LEN([RestrictionGroupName]) > 0
                AND LEN([ActorSubject]) > 0 AND LEN([ActorName]) > 0 AND [ActorSubject] <> [RepSubject]
                AND [Change] IN (0, 1) AND DATEPART(TZOFFSET, [ChangedAt]) = 0
                AND ([Reason] IS NULL OR LEN([Reason]) > 0)
                """.ReplaceLineEndings("\n"));
        });
        changes.HasKey(row => row.Id);
        changes.Property(row => row.Id).ValueGeneratedNever();
        changes.Property(row => row.Sequence).UseIdentityColumn();
        changes.HasIndex(row => row.Sequence).IsUnique();
        changes.HasIndex(row => new { row.RepSubject, row.RestrictionGroupId, row.Sequence });
        changes.HasIndex(row => new { row.RestrictionGroupId, row.Sequence });
        Subject(changes.Property(row => row.RepSubject)).IsRequired();
        Subject(changes.Property(row => row.ActorSubject)).IsRequired();
        changes.Property(row => row.RepName).HasMaxLength(StaffDirectoryContract.MaximumDisplayNameLength).IsRequired();
        changes.Property(row => row.ActorName).HasMaxLength(StaffDirectoryContract.MaximumDisplayNameLength).IsRequired();
        changes.Property(row => row.RestrictionGroupName).HasMaxLength(NamedReferenceItem.MaximumNameLength).IsRequired();
        changes.Property(row => row.Reason).HasMaxLength(1000);
        // Rep, actor and group IDs and names are historical snapshots, not live FKs.
    }

    private static PropertyBuilder<string> Subject(PropertyBuilder<string> property) =>
        property.HasMaxLength(CoverageFields.MaximumSubjectLength).UseCollation("Latin1_General_100_BIN2");
}
