using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShuttleApi.Domain.Common;

namespace ShuttleApi.Infrastructure.Persistence.Configurations;

public sealed class DocumentFileBlobConfiguration : IEntityTypeConfiguration<DocumentFileBlob>
{
    public void Configure(EntityTypeBuilder<DocumentFileBlob> builder)
    {
        builder.ToTable("document_file_blobs");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();

        builder.Property(b => b.StorageKey).HasMaxLength(500).IsRequired();
        builder.HasIndex(b => b.StorageKey).IsUnique();

        builder.Property(b => b.FileName).HasMaxLength(260).IsRequired();
        builder.Property(b => b.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(b => b.FileData).IsRequired();
        builder.Property(b => b.FileSizeBytes).IsRequired();
        builder.Property(b => b.StoredAt).IsRequired();
    }
}
