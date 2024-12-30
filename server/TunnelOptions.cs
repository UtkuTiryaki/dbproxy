namespace server;

public record TunnelOptions
{
    public int ListenPort { get; init; } = 5000;
    public int BufferSize { get; init; } = 8192;
}
