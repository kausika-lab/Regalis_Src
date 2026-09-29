using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Regalis.Application.Operations;

namespace Regalis.Infrastructure.BackgroundJobs;

/// <summary>
/// Every five minutes: checklist tasks still Pending or In progress past their due time
/// (plus Maintenance.OverdueGraceMinutes) turn Overdue, and admins and supervisors get
/// one grouped alert per sweep (Maintenance spec §4.4).
/// </summary>
public class MaintenanceOverdueSweeper(IServiceScopeFactory scopeFactory, ILogger<MaintenanceOverdueSweeper> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IMaintenanceService>().SweepOverdueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                // The sweeper must survive transient failures; the next tick retries.
                logger.LogError(exception, "Maintenance overdue sweep failed");
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
