using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Directory;

// Checkpoint mapping: invoke after Continue T-2.4.1, then generate the migration
// and install the guards written in docs/contact-location-storage.sql.
public static class ContactModelConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        var contacts = modelBuilder.Entity<Contact>();
        contacts.ToTable("Contacts", table =>
        {
            table.HasCheckConstraint("CK_Contacts_Status", "[Status] IN (0,1)");
            table.HasTrigger("TR_Contacts_MainStatus"); table.UseSqlOutputClause(false);
        });
        contacts.HasKey(contact => contact.Id); contacts.Property(contact => contact.Id).ValueGeneratedNever();
        contacts.Property(contact => contact.Name).HasMaxLength(200).IsRequired();
        contacts.Property(contact => contact.Phone).HasMaxLength(Contact.MaximumPhoneLength);
        contacts.Property(contact => contact.Email).HasMaxLength(Contact.MaximumEmailLength);
        contacts.Property(contact => contact.Version).IsRowVersion();
        contacts.HasOne<ContactType>().WithMany().HasForeignKey(contact => contact.ContactTypeId).OnDelete(DeleteBehavior.Restrict);
        contacts.HasIndex(contact => contact.Name);
        contacts.HasMany(contact => contact.Locations).WithOne(link => link.Contact).HasForeignKey(link => link.ContactId).OnDelete(DeleteBehavior.Restrict);
        contacts.Navigation(contact => contact.Locations).HasField("_locations").UsePropertyAccessMode(PropertyAccessMode.Field);

        var links = modelBuilder.Entity<LocationContact>();
        links.ToTable("LocationContacts", table =>
        { table.HasTrigger("TR_LocationContacts_FirstMain"); table.UseSqlOutputClause(false); });
        links.HasKey(link => new { link.LocationId, link.ContactId });
        links.Ignore(link => link.IsMain); links.HasIndex(link => link.ContactId);

        var locations = modelBuilder.Entity<Location>();
        locations.ToTable("Locations", table =>
        { table.HasTrigger("TR_Locations_MainGuard"); table.UseSqlOutputClause(false); });
        locations.Property(location => location.MainContactId);
        locations.HasIndex(location => location.MainContactId);
        locations.HasMany(location => location.Contacts).WithOne(link => link.Location)
            .HasForeignKey(link => link.LocationId).OnDelete(DeleteBehavior.Restrict);
        locations.Navigation(location => location.Contacts).HasField("_contacts").UsePropertyAccessMode(PropertyAccessMode.Field);
        // One scalar choice per Location, constrained to that Location's own link.
        locations.HasOne<LocationContact>().WithMany()
            .HasForeignKey(location => new { location.Id, location.MainContactId })
            .HasPrincipalKey(link => new { link.LocationId, link.ContactId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Locations_MainContactLink");
    }
}
