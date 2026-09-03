using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexaOps.Domain.Entities;

namespace NexaOps.Infrastructure.Persistence.Configurations;

public class CompanyMemberConfiguration : IEntityTypeConfiguration<CompanyMember>
{
    public void Configure(EntityTypeBuilder<CompanyMember> builder)
    {
        builder.ToTable("company_members");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.CompanyId)
            .IsRequired();

        builder.Property(m => m.Email)
            .HasMaxLength(255)
            .IsRequired();

        builder.HasIndex(m => new { m.CompanyId, m.Email })
            .IsUnique();

        builder.HasIndex(m => new { m.CompanyId, m.UserId });

        builder.Property(m => m.Role)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(m => m.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(m => m.InviteToken)
            .HasMaxLength(100);

        builder.HasIndex(m => m.InviteToken);

        builder.HasOne(m => m.Company)
            .WithMany()
            .HasForeignKey(m => m.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(m => m.Department)
            .WithMany()
            .HasForeignKey(m => m.DepartmentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(m => m.InvitedBy)
            .WithMany()
            .HasForeignKey(m => m.InvitedById)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
