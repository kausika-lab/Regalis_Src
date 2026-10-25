using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Regalis.Application.Abstractions;
using Regalis.Application.Reporting.Analytics;
using Regalis.Shared;

namespace Regalis.Infrastructure.BackgroundJobs;

/// <summary>
/// Reports &amp; Analysis in the background (spec §7.4, §7.5, §12.2). Every minute: send
/// the schedules that are due (compared in IST) and build queued large exports. At
/// 02:00 IST: roll yesterday up into RPT_DailyMetric. On start-up it also fills any day
/// the rollup missed, so the overview is fast again after downtime. The spec asks for a
/// 15-minute tick; a one-minute tick sends on time and keeps retries ten minutes apart.
/// </summary>
public class ReportWorker(IServiceScopeFactory scopeFactory, IDateTimeProvider clock, ILogger<ReportWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
    private static readonly TimeOnly NightlyAt = new(2, 0);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        DateOnly? lastNightly = null;
        var firstTick = true;
        using var timer = new PeriodicTimer(Interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            var nowIst = IstClock.ToIst(clock.UtcNow);
            var today = DateOnly.FromDateTime(nowIst);
            if (firstTick || (lastNightly != today && TimeOnly.FromDateTime(nowIst) >= NightlyAt))
            {
                await RunAsync<IDailyMetricsService>(s => s.RunNightlyAsync(stoppingToken), "Daily metrics rollup");
                lastNightly = today;
                firstTick = false;
            }
            await RunAsync<IReportBackgroundService>(s => s.ProcessDueSchedulesAsync(stoppingToken), "Scheduled reports");
            await RunAsync<IReportBackgroundService>(s => s.ProcessExportJobsAsync(stoppingToken), "Background exports");

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

    private async Task RunAsync<T>(Func<T, Task> work, string name) where T : notnull
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            await work(scope.ServiceProvider.GetRequiredService<T>());
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
        catch (Exception exception)
        {
            // The worker must survive; the next tick tries again.
            logger.LogError(exception, "{Job} failed", name);
        }
    }
}
