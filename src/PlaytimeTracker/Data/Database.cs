using InsanityGaming.PlaytimeTracker.Config;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace InsanityGaming.PlaytimeTracker.Data;

public sealed class Database
{
    private readonly string _connectionString;
    private readonly ILogger<Database> _logger;

    public Database(DatabaseConfig config, ILogger<Database> logger)
    {
        var builder = new MySqlConnectionStringBuilder
        {
            Server                  = config.Host,
            Port                    = (uint)config.Port,
            Database                = config.Name,
            UserID                  = config.User,
            Password                = config.Password,
            AllowPublicKeyRetrieval = true,
        };
        _connectionString = builder.ConnectionString;
        _logger = logger;
    }

    public async Task<MySqlConnection> OpenConnectionAsync()
    {
        var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();
        return conn;
    }

    public async Task EnsureSchemaAsync()
    {
        var assembly     = Assembly.GetExecutingAssembly();
        var resourceName = "InsanityGaming.PlaytimeTracker.Data.Schema.sql";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");

        using var reader = new StreamReader(stream);
        var sql = await reader.ReadToEndAsync();

        await using var conn = await OpenConnectionAsync();

        var statements = sql.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var raw in statements)
        {
            var stmt = raw.Trim();
            if (string.IsNullOrWhiteSpace(stmt)) continue;
            await using var cmd = new MySqlCommand(stmt, conn);
            await cmd.ExecuteNonQueryAsync();
        }

        _logger.LogInformation("PlaytimeTracker: database schema verified.");
    }
}
