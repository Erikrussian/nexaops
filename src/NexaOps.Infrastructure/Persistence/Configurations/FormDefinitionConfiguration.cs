using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexaOps.Domain.Entities;

namespace NexaOps.Infrastructure.Persistence.Configurations;

public class FormDefinitionConfiguration : IEntityTypeConfiguration<FormDefinition>
{
    public void Configure(EntityTypeBuilder<FormDefinition> builder)
    {
        builder.ToTable("form_definitions");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Title)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(f => f.Code)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(f => f.Description)
            .HasMaxLength(2000);

        builder.Property(f => f.Status)
            .IsRequired();

        builder.Property(f => f.Version)
            .HasDefaultValue(1);

        builder.Property(f => f.SchemaJson)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(f => f.CompanyId)
            .IsRequired();

        builder.Property(f => f.CreatedById)
            .IsRequired();

        builder.HasIndex(f => new { f.CompanyId, f.Code })
            .IsUnique();

        builder.HasIndex(f => new { f.CompanyId, f.DepartmentId });
        builder.HasIndex(f => new { f.CompanyId, f.Status });

        builder.HasOne(f => f.Company)
            .WithMany()
            .HasForeignKey(f => f.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(f => f.Department)
            .WithMany()
            .HasForeignKey(f => f.DepartmentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(f => f.CreatedBy)
            .WithMany()
            .HasForeignKey(f => f.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
