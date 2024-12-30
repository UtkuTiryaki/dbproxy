using System.Net.Sockets;

namespace server;

public class ConnectionManager : IConnectionManager, IDisposable
{
    private readonly List<TcpClient> _connections = new();
    private readonly object _lock = new();
    private bool _disposed;

    public int ActiveConnectionCount
    {
        get
        {
            lock (_lock)
            {
                return _connections.Count / 2;
            }
        }
    }

    public void AddConnection(TcpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        lock (_lock)
        {
            _connections.Add(client);
        }
    }

    public void RemoveConnection(TcpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        lock (_lock)
        {
            _connections.Remove(client);
            client.Dispose();
        }
    }

    public void CloseAll()
    {
        lock (_lock)
        {
            foreach (var connection in _connections)
            {
                try
                {
                    connection.Dispose();
                }
                catch
                {
                    // Ignore disposal errors
                }
            }
            _connections.Clear();
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            CloseAll();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
