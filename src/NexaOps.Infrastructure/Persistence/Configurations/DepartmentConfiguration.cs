using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexaOps.Domain.Entities;

namespace NexaOps.Infrastructure.Persistence.Configurations;

public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("departments");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Name)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(d => d.Code)
            .HasMaxLength(50);

        builder.Property(d => d.Description)
            .HasMaxLength(2000);

        builder.Property(d => d.CompanyId)
            .IsRequired();

        builder.HasIndex(d => new { d.CompanyId, d.Name });
        builder.HasIndex(d => new { d.CompanyId, d.ParentId });

        builder.HasOne(d => d.Company)
            .WithMany()
            .HasForeignKey(d => d.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.Parent)
            .WithMany(d => d.Children)
            .HasForeignKey(d => d.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.Manager)
            .WithMany()
            .HasForeignKey(d => d.ManagerId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(d => d.IsActive)
            .HasDefaultValue(true);
    }
}
