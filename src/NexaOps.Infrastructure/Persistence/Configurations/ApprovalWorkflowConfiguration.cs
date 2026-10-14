using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexaOps.Domain.Entities;

namespace NexaOps.Infrastructure.Persistence.Configurations;

public class ApprovalWorkflowConfiguration : IEntityTypeConfiguration<ApprovalWorkflow>
{
    public void Configure(EntityTypeBuilder<ApprovalWorkflow> builder)
    {
        builder.ToTable("approval_workflows");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.Name)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(w => w.Description)
            .HasMaxLength(2000);

        builder.Property(w => w.IsActive)
            .HasDefaultValue(true);

        builder.Property(w => w.CompanyId)
            .IsRequired();

        builder.Property(w => w.FormDefinitionId)
            .IsRequired();

        builder.HasIndex(w => new { w.CompanyId, w.FormDefinitionId })
            .IsUnique();

        builder.HasOne(w => w.Company)
            .WithMany()
            .HasForeignKey(w => w.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(w => w.FormDefinition)
            .WithOne(f => f.ApprovalWorkflow)
            .HasForeignKey<ApprovalWorkflow>(w => w.FormDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(w => w.Steps)
            .WithOne(s => s.Workflow)
            .HasForeignKey(s => s.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
