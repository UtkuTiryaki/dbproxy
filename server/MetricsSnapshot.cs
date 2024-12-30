namespace server;

public record MetricsSnapshot
{
    public long TotalBytesUpstream { get; init; }
    public long TotalBytesDownstream { get; init; }
    public int CurrentConnections { get; init; }
    public int TotalConnections { get; init; }
    public double AverageConnectionDurationSeconds { get; init; }
    public double BytesPerSecondUpstream { get; init; }
    public double BytesPerSecondDownstream { get; init; }
    public DateTime SnapshotTime { get; init; } = DateTime.UtcNow;
}
