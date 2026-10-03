using FieldSales.Api.Directory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FieldSales.Api.Coverage;

public static class AssignmentHistoryModelConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        var history = modelBuilder.Entity<AssignmentHistory>();
        history.ToTable("AssignmentHistory", table =>
        {
            table.HasTrigger("TR_AssignmentHistory_AppendOnly");
            table.UseSqlOutputClause(false);
            table.HasCheckConstraint("CK_AssignmentHistory_Change", """
                [Id] <> '00000000-0000-0000-0000-000000000000'
                AND [OperationId] <> '00000000-0000-0000-0000-000000000000'
                AND [LocationId] <> '00000000-0000-0000-0000-000000000000'
                AND LEN([LocationName]) > 0 AND LEN([ActorSubject]) > 0 AND LEN([ActorName]) > 0
                AND [Cause] IN (0, 1, 2) AND DATEPART(TZOFFSET, [ChangedAt]) = 0
                AND ([PreviousRepSubject] IS NOT NULL OR [NewRepSubject] IS NOT NULL)
                AND ([PreviousRepSubject] IS NULL OR [NewRepSubject] IS NULL OR [PreviousRepSubject] <> [NewRepSubject])
                """.ReplaceLineEndings("\n"));
            table.HasCheckConstraint("CK_AssignmentHistory_PreviousOwner", OwnerCheck("Previous"));
            table.HasCheckConstraint("CK_AssignmentHistory_NewOwner", OwnerCheck("New"));
        });
        history.HasKey(row => row.Id);
        history.Property(row => row.Id).ValueGeneratedNever();
        history.Property(row => row.Sequence).UseIdentityColumn();
        history.HasIndex(row => row.Sequence).IsUnique();
        history.HasIndex(row => new { row.OperationId, row.LocationId }).IsUnique();
        history.HasIndex(row => new { row.LocationId, row.Sequence });
        history.HasIndex(row => new { row.PreviousRepSubject, row.Sequence });
        history.HasIndex(row => new { row.NewRepSubject, row.Sequence });
        history.Property(row => row.LocationName).HasMaxLength(200).IsRequired();
        Subject(history.Property(row => row.ActorSubject)).IsRequired();
        Subject(history.Property(row => row.PreviousRepSubject));
        Subject(history.Property(row => row.NewRepSubject));
        history.Property(row => row.ActorName).HasMaxLength(512).IsRequired();
        history.Property(row => row.PreviousRepName).HasMaxLength(512);
        history.Property(row => row.NewRepName).HasMaxLength(512);
        history.Property(row => row.PreviousSourceName).HasMaxLength(200);
        history.Property(row => row.NewSourceName).HasMaxLength(200);
        history.Property(row => row.Reason).HasMaxLength(1000);
        history.HasOne<Location>().WithMany().HasForeignKey(row => row.LocationId).OnDelete(DeleteBehavior.Restrict);
        // Assignment/unit/account IDs and labels are historical snapshots, not live FKs.
    }

    private static PropertyBuilder<T> Subject<T>(PropertyBuilder<T> property) =>
        property.HasMaxLength(450).UseCollation("Latin1_General_100_BIN2");

    private static string OwnerCheck(string prefix) => $"""
        ([{prefix}RepSubject] IS NULL AND [{prefix}RepName] IS NULL AND [{prefix}AssignmentId] IS NULL
          AND [{prefix}Level] IS NULL AND [{prefix}UnitId] IS NULL AND [{prefix}SourceName] IS NULL)
        OR
        ([{prefix}RepSubject] IS NOT NULL AND LEN([{prefix}RepSubject]) > 0
          AND [{prefix}RepName] IS NOT NULL AND LEN([{prefix}RepName]) > 0
          AND [{prefix}AssignmentId] IS NOT NULL AND [{prefix}AssignmentId] <> '00000000-0000-0000-0000-000000000000'
          AND [{prefix}Level] IS NOT NULL AND [{prefix}Level] IN (0, 1, 2, 3)
          AND [{prefix}UnitId] IS NOT NULL AND [{prefix}UnitId] <> '00000000-0000-0000-0000-000000000000'
          AND [{prefix}SourceName] IS NOT NULL AND LEN([{prefix}SourceName]) > 0)
        """.ReplaceLineEndings("\n");
}
