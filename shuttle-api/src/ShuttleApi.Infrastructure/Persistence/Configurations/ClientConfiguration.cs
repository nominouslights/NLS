using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShuttleApi.Domain.Clients;

namespace ShuttleApi.Infrastructure.Persistence.Configurations;

public sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("clients");

        builder.Ignore(c => c.DomainEvents);

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.BusinessName).HasMaxLength(200).IsRequired();
        builder.Property(c => c.ServiceType).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(c => c.PrimaryContactName).HasMaxLength(200).IsRequired();
        builder.Property(c => c.PrimaryContactTitle).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Phone).HasMaxLength(30).IsRequired();
        builder.Property(c => c.Email).HasMaxLength(320).IsRequired();
        builder.Property(c => c.StreetAddress).HasMaxLength(300).IsRequired();
        builder.Property(c => c.City).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Province).HasMaxLength(2).IsRequired();
        builder.Property(c => c.PostalCode).HasMaxLength(7).IsRequired();
        builder.Property(c => c.GstHstNumber).HasMaxLength(20);
        builder.Property(c => c.PreferredPaymentMethod).HasMaxLength(50).IsRequired();
        builder.Property(c => c.NetPaymentTerms).IsRequired();
        builder.Property(c => c.OutstandingBalance).HasPrecision(18, 2).IsRequired();
        builder.Property(c => c.ComplianceNotes).HasMaxLength(2000);
        builder.Property(c => c.IsMinesite).IsRequired();
        builder.Property(c => c.IsActive).IsRequired();
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.Industry).HasMaxLength(200);
        builder.Property(c => c.ProjectSite).HasMaxLength(200);
    }
}
