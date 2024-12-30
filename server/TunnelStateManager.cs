namespace server;

public class TunnelStateManager : ITunnelStateManager
{
    private string? _configuredHost;
    private int? _configuredPort;
    private readonly object _lock = new();

    public bool IsConfigured
    {
        get
        {
            lock (_lock)
            {
                return _configuredHost != null && _configuredPort.HasValue;
            }
        }
    }

    public string? ConfiguredHost
    {
        get
        {
            lock (_lock)
            {
                return _configuredHost;
            }
        }
    }

    public int? ConfiguredPort
    {
        get
        {
            lock (_lock)
            {
                return _configuredPort;
            }
        }
    }

    public void SetConfiguration(string host, int port)
    {
        lock (_lock)
        {
            _configuredHost = host;
            _configuredPort = port;
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _configuredHost = null;
            _configuredPort = null;
        }
    }
}
