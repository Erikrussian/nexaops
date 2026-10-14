using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexaOps.Domain.Entities;

namespace NexaOps.Infrastructure.Persistence.Configurations;

public class ApprovalStepConfiguration : IEntityTypeConfiguration<ApprovalStep>
{
    public void Configure(EntityTypeBuilder<ApprovalStep> builder)
    {
        builder.ToTable("approval_steps");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(s => s.StepType)
            .IsRequired();

        builder.Property(s => s.OrderIndex)
            .IsRequired();

        builder.Property(s => s.AllowSelfApproval)
            .HasDefaultValue(false);

        builder.Property(s => s.IsActive)
            .HasDefaultValue(true);

        builder.HasIndex(s => new { s.WorkflowId, s.OrderIndex })
            .IsUnique();

        builder.HasOne(s => s.SpecificApproverUser)
            .WithMany()
            .HasForeignKey(s => s.SpecificApproverUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
