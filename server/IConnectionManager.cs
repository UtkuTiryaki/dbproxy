using System.Net.Sockets;

namespace server;

public interface IConnectionManager
{
    void AddConnection(TcpClient client);
    void RemoveConnection(TcpClient client);
    int ActiveConnectionCount { get; }
    void CloseAll();
}
