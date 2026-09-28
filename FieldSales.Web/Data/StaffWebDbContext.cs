using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Web.Data;

public sealed class StaffWebDbContext(DbContextOptions<StaffWebDbContext> options)
    : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<StoredTicket> Tickets => Set<StoredTicket>();
    public DbSet<StaffAreaPreference> StaffAreaPreferences => Set<StaffAreaPreference>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StoredTicket>(entity =>
        {
            entity.HasKey(ticket => ticket.Key);
            entity.Property(ticket => ticket.Key).HasMaxLength(64);
            entity.Property(ticket => ticket.ProtectedValue).IsRequired();
            entity.HasIndex(ticket => ticket.ExpiresUtc);
        });
        modelBuilder.Entity<StaffAreaPreference>(entity =>
        {
            entity.HasKey(preference => preference.SubjectId);
            entity.Property(preference => preference.SubjectId).HasMaxLength(256);
            entity.Property(preference => preference.Area).HasMaxLength(32).IsRequired();
        });
    }
}

public sealed class StoredTicket
{
    public string Key { get; set; } = string.Empty;
    public byte[] ProtectedValue { get; set; } = [];
    public DateTimeOffset ExpiresUtc { get; set; }
}

public sealed class StaffAreaPreference
{
    public string SubjectId { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public DateTimeOffset UpdatedUtc { get; set; }
}
