using System.Collections.Concurrent;

namespace server;

public class TunnelMetrics : ITunnelMetrics
{
    private long _totalBytesUpstream;
    private long _totalBytesDownstream;
    private int _currentConnections;
    private int _totalConnections;
    private readonly ConcurrentQueue<double> _connectionDurations = new();
    private readonly ConcurrentDictionary<string, (long Bytes, DateTime LastUpdate)> _throughputData = new();
    private readonly object _lock = new();

    public void IncrementBytesTransferred(string direction, long bytes)
    {
        var now = DateTime.UtcNow;
        if (direction == "upstream")
        {
            Interlocked.Add(ref _totalBytesUpstream, bytes);
            _throughputData.AddOrUpdate(
                "upstream",
                (bytes, now),
                (_, old) => (old.Bytes + bytes, now)
            );
        }
        else
        {
            Interlocked.Add(ref _totalBytesDownstream, bytes);
            _throughputData.AddOrUpdate(
                "downstream",
                (bytes, now),
                (_, old) => (old.Bytes + bytes, now)
            );
        }
    }

    public void IncrementConnections()
    {
        lock (_lock)
        {
            _currentConnections++;
            _totalConnections++;
        }
    }

    public void DecrementConnections()
    {
        lock (_lock)
        {
            _currentConnections--;
        }
    }

    public void AddConnectionDuration(TimeSpan duration)
    {
        _connectionDurations.Enqueue(duration.TotalSeconds);
        while (_connectionDurations.Count > 1000) // Behalte nur die letzten 1000 Verbindungen
        {
            _connectionDurations.TryDequeue(out _);
        }
    }

    public MetricsSnapshot GetSnapshot()
    {
        var now = DateTime.UtcNow;
        
        // Berechne Durchschnittliche Verbindungsdauer
        var durations = _connectionDurations.ToArray();
        var avgDuration = durations.Length > 0 ? durations.Average() : 0;

        // Berechne Bytes pro Sekunde
        double bytesPerSecondUpstream = 0;
        double bytesPerSecondDownstream = 0;

        if (_throughputData.TryGetValue("upstream", out var upstreamData))
        {
            var timeDiff = (now - upstreamData.LastUpdate).TotalSeconds;
            if (timeDiff > 0)
            {
                bytesPerSecondUpstream = upstreamData.Bytes / timeDiff;
            }
        }

        if (_throughputData.TryGetValue("downstream", out var downstreamData))
        {
            var timeDiff = (now - downstreamData.LastUpdate).TotalSeconds;
            if (timeDiff > 0)
            {
                bytesPerSecondDownstream = downstreamData.Bytes / timeDiff;
            }
        }

        return new MetricsSnapshot
        {
            TotalBytesUpstream = Interlocked.Read(ref _totalBytesUpstream),
            TotalBytesDownstream = Interlocked.Read(ref _totalBytesDownstream),
            CurrentConnections = _currentConnections,
            TotalConnections = _totalConnections,
            AverageConnectionDurationSeconds = avgDuration,
            BytesPerSecondUpstream = bytesPerSecondUpstream,
            BytesPerSecondDownstream = bytesPerSecondDownstream
        };
    }
}
