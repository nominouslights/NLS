using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShuttleApi.Domain.Drivers;

namespace ShuttleApi.Infrastructure.Persistence.Configurations;

public sealed class DriverConfiguration : IEntityTypeConfiguration<Driver>
{
    public void Configure(EntityTypeBuilder<Driver> builder)
    {
        builder.ToTable("drivers");

        builder.Ignore(d => d.DomainEvents);

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.EmployeeId).HasMaxLength(20).IsRequired();
        builder.HasIndex(d => d.EmployeeId).IsUnique();

        builder.Property(d => d.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(d => d.LastName).HasMaxLength(100).IsRequired();
        builder.Property(d => d.Phone).HasMaxLength(30).IsRequired();
        builder.Property(d => d.Email).HasMaxLength(320).IsRequired();
        builder.Property(d => d.HireDate).IsRequired();
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(d => d.IsActive).IsRequired();
        builder.Property(d => d.CreatedAt).IsRequired();
        builder.Property(d => d.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(d => d.DeletedAt);

        builder.Ignore(d => d.FullName);

        builder.HasMany(d => d.Documents)
            .WithOne()
            .HasForeignKey(doc => doc.DriverId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(d => d.RosterEntries)
            .WithOne()
            .HasForeignKey(r => r.DriverId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(d => !d.IsDeleted);
    }
}
