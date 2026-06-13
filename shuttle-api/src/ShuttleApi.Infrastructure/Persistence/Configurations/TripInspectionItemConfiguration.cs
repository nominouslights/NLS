using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Infrastructure.Persistence.Configurations;

public sealed class TripInspectionItemConfiguration : IEntityTypeConfiguration<TripInspectionItem>
{
    public void Configure(EntityTypeBuilder<TripInspectionItem> builder)
    {
        builder.ToTable("trip_inspection_items");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.Property(i => i.PreInspectionId).IsRequired();
        builder.Property(i => i.ItemName).HasMaxLength(200).IsRequired();
        builder.Property(i => i.Category)
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();
        builder.Property(i => i.Passed).IsRequired();
        builder.Property(i => i.Notes).HasMaxLength(1000);

        builder.HasIndex(i => i.PreInspectionId);
    }
}
