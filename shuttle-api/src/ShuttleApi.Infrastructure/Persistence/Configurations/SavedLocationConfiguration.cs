using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShuttleApi.Domain.Locations;

namespace ShuttleApi.Infrastructure.Persistence.Configurations;

public sealed class SavedLocationConfiguration : IEntityTypeConfiguration<SavedLocation>
{
    public void Configure(EntityTypeBuilder<SavedLocation> builder)
    {
        builder.ToTable("saved_locations");

        builder.Ignore(l => l.DomainEvents);

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();

        builder.Property(l => l.Name).HasMaxLength(200).IsRequired();
        builder.Property(l => l.Address).HasMaxLength(500);
        builder.Property(l => l.Latitude);
        builder.Property(l => l.Longitude);
        builder.Property(l => l.CreatedAt).IsRequired();
        builder.Property(l => l.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(l => l.DeletedAt);

        builder.HasIndex(l => l.Name);
    }
}
