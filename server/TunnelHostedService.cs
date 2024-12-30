using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace server;

public class TunnelHostedService : BackgroundService
{
	private readonly ILogger<TunnelHostedService> _logger;
	private readonly TunnelOptions _options;
	private readonly ITunnelStateManager _stateManager;
	private readonly IConnectionManager _connectionManager;
	private readonly ITunnelMetrics _tunnelMetrics;
	private readonly ConcurrentDictionary<TcpClient, DateTime> _connectionStartTimes = new();
	private TcpListener? _listener;

	public TunnelHostedService(
		ILogger<TunnelHostedService> logger,
		IOptions<TunnelOptions> options,
		ITunnelStateManager stateManager,
		IConnectionManager connectionManager,
		ITunnelMetrics tunnelMetrics)
	{
		_logger = logger;
		_options = options.Value;
		_stateManager = stateManager;
		_connectionManager = connectionManager;
		_tunnelMetrics = tunnelMetrics;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_listener = new TcpListener(IPAddress.Any, _options.ListenPort);
		_listener.Start();

		_logger.LogInformation("TCP Tunnel gestartet auf Port {Port}", _options.ListenPort);

		try
		{
			while (!stoppingToken.IsCancellationRequested)
			{
				var client = await _listener.AcceptTcpClientAsync(stoppingToken);

				if (!_stateManager.IsConfigured)
				{
					_ = HandleConfigurationAsync(client, stoppingToken);
				}
				else
				{
					_ = HandleDataConnectionAsync(client, stoppingToken);
				}
			}
		}
		finally
		{
			_listener.Stop();
		}
	}

	private async Task HandleConfigurationAsync(TcpClient client, CancellationToken cancellationToken)
	{
		try
		{
			var stream = client.GetStream();

			// Lese Host-Länge
			byte[] hostLengthBytes = new byte[4];
			await stream.ReadAsync(hostLengthBytes, cancellationToken);
			int hostLength = BitConverter.ToInt32(hostLengthBytes);

			// Lese Host
			byte[] hostBytes = new byte[hostLength];
			await stream.ReadAsync(hostBytes, cancellationToken);
			string targetHost = System.Text.Encoding.UTF8.GetString(hostBytes);

			// Lese Port
			byte[] portBytes = new byte[4];
			await stream.ReadAsync(portBytes, cancellationToken);
			int targetPort = BitConverter.ToInt32(portBytes);

			var endpoint = (IPEndPoint)client.Client.RemoteEndPoint!;
			_logger.LogInformation(
				"Neue Konfiguration von {Address}:{Port} für Ziel {TargetHost}:{TargetPort}",
				endpoint.Address, endpoint.Port, targetHost, targetPort);

			_stateManager.SetConfiguration(targetHost, targetPort);

			// Sende Bestätigung
			await stream.WriteAsync(new byte[] { 1 }, cancellationToken);
			await stream.FlushAsync(cancellationToken);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "Fehler bei der Konfiguration");
		}
		finally
		{
			client.Dispose();
		}
	}

	private async Task HandleDataConnectionAsync(TcpClient client, CancellationToken cancellationToken)
	{
		var targetHost = _stateManager.ConfiguredHost;
		var targetPort = _stateManager.ConfiguredPort;

		if (targetHost == null || !targetPort.HasValue)
		{
			_logger.LogError("Keine gültige Zielkonfiguration vorhanden");
			client.Dispose();
			return;
		}

		TcpClient? target = null;
		try
		{
			var endpoint = (IPEndPoint)client.Client.RemoteEndPoint!;
			_logger.LogInformation(
				"Neue Datenverbindung von {Address}:{Port}",
				endpoint.Address, endpoint.Port);

			target = new TcpClient();
			await target.ConnectAsync(targetHost, targetPort.Value, cancellationToken);

			_connectionManager.AddConnection(client);
			_connectionManager.AddConnection(target);
			
			_tunnelMetrics.IncrementConnections();
			_connectionStartTimes.TryAdd(client, DateTime.UtcNow);

			var forwardTask = ForwardDataAsync(
				client.GetStream(),
				target.GetStream(),
				"Client → Target",
				cancellationToken);

			var backwardTask = ForwardDataAsync(
				target.GetStream(),
				client.GetStream(),
				"Target → Client",
				cancellationToken);

			await Task.WhenAll(forwardTask, backwardTask);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "Verbindungsfehler");
		}
		finally
		{
			if (target != null) _connectionManager.RemoveConnection(target);
			_connectionManager.RemoveConnection(client);
			
			if (_connectionStartTimes.TryRemove(client, out var startTime))
            {
                _tunnelMetrics.AddConnectionDuration(DateTime.UtcNow - startTime);
            }
            _tunnelMetrics.DecrementConnections();

			_logger.LogInformation("Verbindung geschlossen. Aktive Verbindungen: {Count}",
				_connectionManager.ActiveConnectionCount);
		}
	}

	private async Task ForwardDataAsync(
		NetworkStream source,
		NetworkStream destination,
		string direction,
		CancellationToken cancellationToken)
	{
		var buffer = new byte[_options.BufferSize];
		var metricDirection = direction.Contains("→") ? "upstream" : "downstream";
		try
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				int bytesRead = await source.ReadAsync(buffer, cancellationToken);
				if (bytesRead == 0) break;

				await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
				await destination.FlushAsync(cancellationToken);

				_tunnelMetrics.IncrementBytesTransferred(metricDirection, bytesRead);
				_logger.LogDebug("{Direction}: {BytesRead} Bytes übertragen", direction, bytesRead);
			}
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "Fehler beim Weiterleiten der Daten ({Direction})", direction);
		}
	}

	public override async Task StopAsync(CancellationToken cancellationToken)
	{
		_listener?.Stop();
		_connectionManager.CloseAll();
		await base.StopAsync(cancellationToken);
	}
}