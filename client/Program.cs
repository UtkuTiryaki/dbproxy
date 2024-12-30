using Npgsql;
using System.Net.Sockets;

namespace Client;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("PostgreSQL Client");
        Console.WriteLine("----------------");

        try
        {
            string tunnelHost = "localhost";
            int tunnelPort = 5000;
            string targetHost = "localhost";
            int targetPort = 5432;

            // 1. Konfigurationsverbindung herstellen
            using (var configClient = new TcpClient())
            {
                Console.WriteLine("Sende Konfiguration zum Tunnel...");
                await configClient.ConnectAsync(tunnelHost, tunnelPort);
                
                var stream = configClient.GetStream();
                
                // Sende Host-Länge und Host
                byte[] hostBytes = System.Text.Encoding.ASCII.GetBytes(targetHost);
                await stream.WriteAsync(BitConverter.GetBytes(hostBytes.Length));
                await stream.WriteAsync(hostBytes);
                
                // Sende Port
                await stream.WriteAsync(BitConverter.GetBytes(targetPort));
                await stream.FlushAsync();
                
                // Warte kurz, bis der Tunnel konfiguriert ist
                await Task.Delay(1000);
            }
            
            Console.WriteLine("Tunnel konfiguriert");

            // 2. Neue Verbindung für PostgreSQL
            Console.WriteLine("Stelle Datenbankverbindung her...");
            var connectionString = new NpgsqlConnectionStringBuilder
            {
                Host = tunnelHost,
                Port = tunnelPort,
                Database = "postgres",
                Username = "testuser",
                Password = "testpass",
                IncludeErrorDetail = true,
                Timeout = 30,
                CommandTimeout = 30
            }.ToString();

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            Console.WriteLine("Datenbankverbindung hergestellt!");

            // Test-Query
            await using var testCmd = new NpgsqlCommand("SELECT version()", connection);
            var version = await testCmd.ExecuteScalarAsync();
            Console.WriteLine($"PostgreSQL Version: {version}");

            // Interaktiver Modus
            while (true)
            {
                try
                {
                    Console.Write("\n> ");
                    var input = await Console.In.ReadLineAsync() ?? string.Empty;

                    if (input.ToLower() == "exit")
                        break;

                    if (string.IsNullOrWhiteSpace(input))
                        continue;

                    // Führe Query aus
                    await using var cmd = new NpgsqlCommand(input, connection);

                    if (input.TrimStart().ToUpper().StartsWith("SELECT"))
                    {
                        await using var reader = await cmd.ExecuteReaderAsync();
                        var fieldCount = reader.FieldCount;
                        
                        // Header
                        for (int i = 0; i < fieldCount; i++)
                        {
                            Console.Write(reader.GetName(i).PadRight(20));
                        }
                        Console.WriteLine("\n" + new string('-', fieldCount * 20));
                        
                        // Daten
                        while (await reader.ReadAsync())
                        {
                            for (int i = 0; i < fieldCount; i++)
                            {
                                var value = reader.IsDBNull(i) ? "NULL" : reader.GetValue(i).ToString();
                                Console.Write(value?.PadRight(20));
                            }
                            Console.WriteLine();
                        }
                    }
                    else
                    {
                        var rowsAffected = await cmd.ExecuteNonQueryAsync();
                        Console.WriteLine($"Befehl ausgeführt. Betroffene Zeilen: {rowsAffected}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Query-Fehler: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Details: {ex.InnerException.Message}");
            }
        }

        Console.WriteLine("\nClient beendet");
    }
}