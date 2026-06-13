using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShuttleApi.Domain.Passengers;

namespace ShuttleApi.Infrastructure.Persistence.Configurations;

public sealed class PassengerProfileConfiguration : IEntityTypeConfiguration<PassengerProfile>
{
    public void Configure(EntityTypeBuilder<PassengerProfile> builder)
    {
        builder.ToTable("passenger_profiles");

        builder.Ignore(p => p.DomainEvents);

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.ClientId).IsRequired();
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.NormalizedName).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Phone).HasMaxLength(20);
        builder.Property(p => p.Email).HasMaxLength(200);
        builder.Property(p => p.LastBookedAt).IsRequired();
        builder.Property(p => p.CreatedAt).IsRequired();

        // One profile per passenger name per client
        builder.HasIndex(p => new { p.ClientId, p.NormalizedName }).IsUnique();

        // For efficient purge queries
        builder.HasIndex(p => p.LastBookedAt);
    }
}
