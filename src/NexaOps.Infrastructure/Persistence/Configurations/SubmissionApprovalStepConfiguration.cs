using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexaOps.Domain.Entities;

namespace NexaOps.Infrastructure.Persistence.Configurations;

public class SubmissionApprovalStepConfiguration : IEntityTypeConfiguration<SubmissionApprovalStep>
{
    public void Configure(EntityTypeBuilder<SubmissionApprovalStep> builder)
    {
        builder.ToTable("submission_approval_steps");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.StepName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(s => s.StepType)
            .IsRequired();

        builder.Property(s => s.OrderIndex)
            .IsRequired();

        builder.Property(s => s.Status)
            .IsRequired();

        builder.Property(s => s.ReviewNotes)
            .HasMaxLength(2000);

        builder.HasIndex(s => new { s.SubmissionId, s.OrderIndex })
            .IsUnique();

        builder.HasOne(s => s.Submission)
            .WithMany(sub => sub.ApprovalSteps)
            .HasForeignKey(s => s.SubmissionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.SpecificApproverUser)
            .WithMany()
            .HasForeignKey(s => s.SpecificApproverUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(s => s.ReviewedBy)
            .WithMany()
            .HasForeignKey(s => s.ReviewedById)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
