using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShuttleApi.Domain.CommunityCalendar;

namespace ShuttleApi.Infrastructure.Persistence.Configurations;

public sealed class CommunityCalendarBlockConfiguration : IEntityTypeConfiguration<CommunityCalendarBlock>
{
    public void Configure(EntityTypeBuilder<CommunityCalendarBlock> builder)
    {
        builder.ToTable("community_calendar_blocks");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();

        builder.Property(b => b.BlockedDate).IsRequired();
        builder.Property(b => b.Reason).HasMaxLength(500).IsRequired();
        builder.Property(b => b.BlockedAt).IsRequired();

        builder.HasIndex(b => b.BlockedDate).IsUnique();
    }
}
