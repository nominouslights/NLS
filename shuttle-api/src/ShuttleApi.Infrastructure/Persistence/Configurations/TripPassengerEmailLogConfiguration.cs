using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Infrastructure.Persistence.Configurations;

public sealed class TripPassengerEmailLogConfiguration : IEntityTypeConfiguration<TripPassengerEmailLog>
{
    public void Configure(EntityTypeBuilder<TripPassengerEmailLog> builder)
    {
        builder.ToTable("trip_passenger_email_logs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.RecipientEmail).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Direction).HasMaxLength(20).IsRequired();
        builder.Property(x => x.SentAt).IsRequired();
        builder.Property(x => x.IsTest).IsRequired();
        builder.HasOne<TripPassenger>()
            .WithMany(p => p.EmailLogs)
            .HasForeignKey(x => x.TripPassengerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
