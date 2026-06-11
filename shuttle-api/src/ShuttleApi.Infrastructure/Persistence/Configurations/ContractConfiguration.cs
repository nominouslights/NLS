using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShuttleApi.Domain.Clients;

namespace ShuttleApi.Infrastructure.Persistence.Configurations;

public sealed class ContractConfiguration : IEntityTypeConfiguration<Contract>
{
    public void Configure(EntityTypeBuilder<Contract> builder)
    {
        builder.ToTable("contracts");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.ClientId).IsRequired();
        builder.Property(c => c.StartDate).IsRequired();
        builder.Property(c => c.EndDate).IsRequired();
        builder.Property(c => c.IsActive).IsRequired();
        builder.Property(c => c.Notes).HasMaxLength(2000);

        builder.Ignore(c => c.IsExpiringSoon);

        builder.HasIndex(c => new { c.ClientId, c.IsActive });

        builder.HasOne<Client>()
            .WithMany()
            .HasForeignKey(c => c.ClientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.RateLines)
            .WithOne()
            .HasForeignKey(r => r.ContractId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
