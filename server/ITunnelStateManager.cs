namespace server;

public interface ITunnelStateManager
{
    bool IsConfigured { get; }
    string? ConfiguredHost { get; }
    int? ConfiguredPort { get; }
    void SetConfiguration(string host, int port);
    void Reset();
}
