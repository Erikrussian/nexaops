using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexaOps.Domain.Entities;

namespace NexaOps.Infrastructure.Persistence.Configurations;

public class FormSubmissionConfiguration : IEntityTypeConfiguration<FormSubmission>
{
    public void Configure(EntityTypeBuilder<FormSubmission> builder)
    {
        builder.ToTable("form_submissions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.DataJson)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(s => s.Status)
            .IsRequired();

        builder.Property(s => s.ReviewNotes)
            .HasMaxLength(2000);

        builder.Property(s => s.CompanyId)
            .IsRequired();

        builder.Property(s => s.FormDefinitionId)
            .IsRequired();

        builder.Property(s => s.SubmittedById)
            .IsRequired();

        builder.HasIndex(s => new { s.CompanyId, s.FormDefinitionId });
        builder.HasIndex(s => new { s.CompanyId, s.SubmittedById });
        builder.HasIndex(s => new { s.CompanyId, s.Status });

        builder.HasOne(s => s.FormDefinition)
            .WithMany()
            .HasForeignKey(s => s.FormDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Company)
            .WithMany()
            .HasForeignKey(s => s.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.SubmittedBy)
            .WithMany()
            .HasForeignKey(s => s.SubmittedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.ReviewedBy)
            .WithMany()
            .HasForeignKey(s => s.ReviewedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
