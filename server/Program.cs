using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace server;

public class Program
{
	public static async Task Main(string[] args)
	{
		var host = Host.CreateDefaultBuilder(args)
			.ConfigureServices((context, services) =>
			{
				services.Configure<TunnelOptions>(context.Configuration.GetSection("Tunnel")); // optional
				services.AddSingleton<ITunnelStateManager, TunnelStateManager>();
				services.AddSingleton<ITunnelMetrics, TunnelMetrics>();
				services.AddSingleton<IConnectionManager, ConnectionManager>();
				services.AddHostedService<TunnelHostedService>();
				services.AddHostedService<MetricsHostedService>();
			})
			.ConfigureLogging((context, logging) =>
			{
				logging.ClearProviders();
				logging.AddConsole();
			})
			.Build();

		await host.RunAsync();
	}
}
