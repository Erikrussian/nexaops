using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexaOps.Domain.Entities;

namespace NexaOps.Infrastructure.Persistence.Configurations;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("companies");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(c => c.Slug)
            .HasMaxLength(255)
            .IsRequired();

        builder.HasIndex(c => c.Slug)
            .IsUnique();

        builder.Property(c => c.Code)
            .HasMaxLength(50);

        builder.Property(c => c.Logo)
            .HasMaxLength(1000);

        builder.Property(c => c.Description)
            .HasMaxLength(2000);

        builder.Property(c => c.OwnerId)
            .IsRequired();

        builder.HasIndex(c => c.OwnerId);

        builder.HasOne(c => c.Owner)
            .WithMany()
            .HasForeignKey(c => c.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(c => c.IsActive)
            .HasDefaultValue(true);
    }
}
