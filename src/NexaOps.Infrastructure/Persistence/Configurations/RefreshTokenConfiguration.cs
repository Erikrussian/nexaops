using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexaOps.Domain.Entities;

namespace NexaOps.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(rt => rt.Id);

        builder.Property(rt => rt.TokenHash)
            .HasMaxLength(256)
            .IsRequired();

        builder.HasIndex(rt => rt.TokenHash)
            .IsUnique();

        builder.Property(rt => rt.CreatedByIp)
            .HasMaxLength(100);

        builder.Property(rt => rt.RevokedByIp)
            .HasMaxLength(100);

        builder.Property(rt => rt.UserAgent)
            .HasMaxLength(1000);

        builder.Property(rt => rt.ReplacedByTokenHash)
            .HasMaxLength(256);

        builder.Property(rt => rt.ReasonRevoked)
            .HasMaxLength(255);

        builder.HasOne(rt => rt.User)
            .WithMany()
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(rt => rt.UserId);
    }
}
