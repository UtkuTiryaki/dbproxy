using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace server;

public class MetricsHostedService : BackgroundService
{
    private readonly ILogger<MetricsHostedService> _logger;
    private readonly ITunnelMetrics _metrics;

    public MetricsHostedService(
        ILogger<MetricsHostedService> logger,
        ITunnelMetrics metrics)
    {
        _logger = logger;
        _metrics = metrics;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var snapshot = _metrics.GetSnapshot();
            var upstreamMB = FormatBytes(snapshot.TotalBytesUpstream);
            var downstreamMB = FormatBytes(snapshot.TotalBytesDownstream);
            var upstreamSpeed = FormatBytes((long)snapshot.BytesPerSecondUpstream);
            var downstreamSpeed = FormatBytes((long)snapshot.BytesPerSecondDownstream);

            _logger.LogInformation(
                "Metriken:\n" +
                "  Upstream: {UpstreamTotal}/s ({UpstreamSpeed}/s)\n" +
                "  Downstream: {DownstreamTotal}/s ({DownstreamSpeed}/s)\n" +
                "  Verbindungen: {CurrentConns} aktiv, {TotalConns} gesamt\n" +
                "  Durchschn. Dauer: {AvgDuration:F1}s",
                upstreamMB, upstreamSpeed,
                downstreamMB, downstreamSpeed,
                snapshot.CurrentConnections, snapshot.TotalConnections,
                snapshot.AverageConnectionDurationSeconds);

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] suffix = { "B", "KB", "MB", "GB", "TB" };
        int i;
        double dblBytes = bytes;
        for (i = 0; i < suffix.Length && bytes >= 1024; i++, bytes /= 1024)
        {
            dblBytes = bytes / 1024.0;
        }
        return $"{dblBytes:0.##} {suffix[i]}";
    }
}