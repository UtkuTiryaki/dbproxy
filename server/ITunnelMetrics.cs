namespace server;

public interface ITunnelMetrics
{
    void IncrementBytesTransferred(string direction, long bytes);
    void IncrementConnections();
    void DecrementConnections();
    void AddConnectionDuration(TimeSpan duration);
    MetricsSnapshot GetSnapshot();
}
