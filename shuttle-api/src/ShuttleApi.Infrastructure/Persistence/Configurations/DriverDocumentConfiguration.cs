using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShuttleApi.Domain.Drivers;

namespace ShuttleApi.Infrastructure.Persistence.Configurations;

public sealed class DriverDocumentConfiguration : IEntityTypeConfiguration<DriverDocument>
{
    public void Configure(EntityTypeBuilder<DriverDocument> builder)
    {
        builder.ToTable("driver_documents");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.DriverId).IsRequired();
        builder.Property(d => d.DocumentType).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(d => d.FileName).HasMaxLength(260).IsRequired();
        builder.Property(d => d.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(d => d.StorageKey).HasMaxLength(500).IsRequired();
        builder.Property(d => d.FileSizeBytes).IsRequired();
        builder.Property(d => d.UploadedAt).IsRequired();
        builder.Property(d => d.ExpiryDate);

        // Drug & Alcohol Test fields
        builder.Property(d => d.TestDate);
        builder.Property(d => d.TestResultValue).HasConversion<string>().HasMaxLength(20);
        builder.Property(d => d.TestedBy).HasMaxLength(200);

        // Driver's License fields
        builder.Property(d => d.LicenseNumber).HasMaxLength(50);
        builder.Property(d => d.LicenseClass).HasConversion<string>().HasMaxLength(10);
        builder.Property(d => d.IssuedDate);
        builder.Property(d => d.LicenseProvince).HasMaxLength(2);

        // Police Record Check fields
        builder.Property(d => d.CheckResultValue).HasConversion<string>().HasMaxLength(20);
        builder.Property(d => d.IssuingAuthority).HasMaxLength(200);

        // Driver Abstract fields
        builder.Property(d => d.ViolationCount);
        builder.Property(d => d.AtFaultAccidentCount);

        builder.Property(d => d.Notes).HasMaxLength(2000);

        builder.Ignore(d => d.IsExpiringSoon);

        builder.HasIndex(d => new { d.DriverId, d.DocumentType });
    }
}
