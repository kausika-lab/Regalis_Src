using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Regalis.Infrastructure.BackgroundJobs;

/// <summary>Runs <see cref="T:Regalis.Infrastructure.BackgroundJobs.CustomerNotificationEmailJobs" /> every minute.</summary>
public class CustomerNotificationEmailWorker(IServiceScopeFactory scopeFactory, ILogger<CustomerNotificationEmailWorker> logger) : BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		using PeriodicTimer timer = new PeriodicTimer(TimeSpan.FromMinutes(1.0));
		do
		{
			try
			{
				using IServiceScope scope = scopeFactory.CreateScope();
				await scope.ServiceProvider.GetRequiredService<CustomerNotificationEmailJobs>().DeliverPendingAsync(stoppingToken);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception exception)
			{
				logger.LogError(exception, "Customer notification email job failed");
			}
		}
		while (await timer.WaitForNextTickAsync(stoppingToken));
	}
}
