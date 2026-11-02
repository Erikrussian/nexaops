using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NexaOps.Application.Common.Interfaces;
using NexaOps.Application.Workflows.Services;

namespace NexaOps.Infrastructure.Services;

/// <summary>
/// Background service executing recurring scans for overdue approval workflow steps across all active companies,
/// triggering automated SLA escalation, audit logging, and realtime notifications.
/// </summary>
public class WorkflowSlaEscalationBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WorkflowSlaEscalationBackgroundService> _logger;
    private readonly TimeSpan _checkInterval;

    public WorkflowSlaEscalationBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<WorkflowSlaEscalationBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _checkInterval = TimeSpan.FromMinutes(2); // Runs every 2 minutes
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Workflow SLA Escalation Background Service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunEscalationSweepAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error occurred during periodic workflow SLA escalation sweep.");
            }

            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Workflow SLA Escalation Background Service is stopping.");
    }

    public async Task<int> RunEscalationSweepAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var workflowService = scope.ServiceProvider.GetRequiredService<IApprovalWorkflowService>();

        // Query distinct company IDs with active form submissions
        var companyIds = context.Companies
            .Select(c => c.Id)
            .ToList();

        var totalEscalated = 0;

        foreach (var companyId in companyIds)
        {
            try
            {
                var count = await workflowService.EscalateOverdueStepsAsync(companyId, cancellationToken);
                if (count > 0)
                {
                    totalEscalated += count;
                    _logger.LogInformation("Escalated {Count} overdue workflow steps for company {CompanyId}", count, companyId);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to run SLA escalation sweep for company {CompanyId}", companyId);
            }
        }

        return totalEscalated;
    }
}
