using Dapper;
using InsanityGaming.ModSharp.PlaytimeTracker.Shared;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Sharp.Shared.Enums;
using Sharp.Shared.Units;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace InsanityGaming.PlaytimeTracker.Data;

public sealed class PlaytimeRepository
{
    private readonly Database _db;
    private readonly ILogger<PlaytimeRepository> _logger;

    public PlaytimeRepository(Database db, ILogger<PlaytimeRepository> logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task UpsertPlayerAsync(
        ulong steamId64,
        string name,
        int totalDeltaSeconds,
        int ctDeltaSeconds,
        int teDeltaSeconds,
        int specDeltaSeconds)
    {
        const string sql = """
            INSERT INTO `pt_players`
                (`steamid64`, `name`, `first_seen`, `last_seen`, `total_seconds`, `ct_seconds`, `te_seconds`, `spec_seconds`)
            VALUES
                (@steamId64, @name, UTC_TIMESTAMP(), UTC_TIMESTAMP(), @total, @ct, @te, @spec)
            ON DUPLICATE KEY UPDATE
                `name`          = @name,
                `last_seen`     = UTC_TIMESTAMP(),
                `total_seconds` = `total_seconds` + @total,
                `ct_seconds`    = `ct_seconds`    + @ct,
                `te_seconds`    = `te_seconds`    + @te,
                `spec_seconds`  = `spec_seconds`  + @spec
            """;

        await using var conn = await _db.OpenConnectionAsync();
        await conn.ExecuteAsync(sql, new
        {
            steamId64,
            name,
            total = totalDeltaSeconds,
            ct    = ctDeltaSeconds,
            te    = teDeltaSeconds,
            spec  = specDeltaSeconds,
        });
    }

    public async Task UpsertServerPlaytimeAsync(
        ulong steamId64,
        int serverId,
        int totalDeltaSeconds,
        int ctDeltaSeconds,
        int teDeltaSeconds,
        int specDeltaSeconds)
    {
        const string sql = """
            INSERT INTO `pt_playtime`
                (`steamid64`, `server_id`, `total_seconds`, `ct_seconds`, `te_seconds`, `spec_seconds`, `last_seen`)
            VALUES
                (@steamId64, @serverId, @total, @ct, @te, @spec, UTC_TIMESTAMP())
            ON DUPLICATE KEY UPDATE
                `total_seconds` = `total_seconds` + @total,
                `ct_seconds`    = `ct_seconds`    + @ct,
                `te_seconds`    = `te_seconds`    + @te,
                `spec_seconds`  = `spec_seconds`  + @spec,
                `last_seen`     = UTC_TIMESTAMP()
            """;

        await using var conn = await _db.OpenConnectionAsync();
        await conn.ExecuteAsync(sql, new
        {
            steamId64,
            serverId,
            total = totalDeltaSeconds,
            ct    = ctDeltaSeconds,
            te    = teDeltaSeconds,
            spec  = specDeltaSeconds,
        });
    }

    public async Task<long> InsertSessionAsync(ulong steamId64, int serverId, DateTime startedUtc)
    {
        const string sql = """
            INSERT INTO `pt_sessions`
                (`steamid64`, `server_id`, `started_utc`, `ended_utc`, `duration_seconds`, `end_reason`)
            VALUES
                (@steamId64, @serverId, @startedUtc, NULL, 0, NULL)
            """;

        await using var conn = await _db.OpenConnectionAsync();
        await conn.ExecuteAsync(sql, new { steamId64, serverId, startedUtc });
        return await conn.ExecuteScalarAsync<long>("SELECT LAST_INSERT_ID()");
    }

    public async Task FinalizeSessionAsync(long sessionId, DateTime endedUtc, int durationSeconds, string endReason)
    {
        const string sql = """
            UPDATE `pt_sessions`
            SET `ended_utc`        = @endedUtc,
                `duration_seconds` = @durationSeconds,
                `end_reason`       = @endReason
            WHERE `id` = @sessionId
            """;

        await using var conn = await _db.OpenConnectionAsync();
        await conn.ExecuteAsync(sql, new { sessionId, endedUtc, durationSeconds, endReason });
    }

    public async Task<TimeSpan> GetGlobalPlaytimeAsync(ulong steamId64, CancellationToken ct = default)
    {
        const string sql = "SELECT `total_seconds` FROM `pt_players` WHERE `steamid64` = @steamId64";

        await using var conn = await _db.OpenConnectionAsync();
        var seconds = await conn.QueryFirstOrDefaultAsync<long?>(
            new CommandDefinition(sql, new { steamId64 }, cancellationToken: ct));

        return TimeSpan.FromSeconds(seconds ?? 0);
    }

    public async Task<TimeSpan> GetServerPlaytimeAsync(ulong steamId64, int serverId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT `total_seconds`
            FROM `pt_playtime`
            WHERE `steamid64` = @steamId64
              AND `server_id` = @serverId
            """;

        await using var conn = await _db.OpenConnectionAsync();
        var seconds = await conn.QueryFirstOrDefaultAsync<long?>(
            new CommandDefinition(sql, new { steamId64, serverId }, cancellationToken: ct));

        return TimeSpan.FromSeconds(seconds ?? 0);
    }

    public async Task<TimeSpan> GetTeamPlaytimeGlobalAsync(ulong steamId64, CStrikeTeam team, CancellationToken ct = default)
    {
        var column = TeamColumn(team);
        var sql = $"SELECT `{column}` FROM `pt_players` WHERE `steamid64` = @steamId64";

        await using var conn = await _db.OpenConnectionAsync();
        var seconds = await conn.QueryFirstOrDefaultAsync<long?>(
            new CommandDefinition(sql, new { steamId64 }, cancellationToken: ct));

        return TimeSpan.FromSeconds(seconds ?? 0);
    }

    public async Task<IReadOnlyList<PlaytimeEntry>> GetTopGlobalAsync(int count, CancellationToken ct = default)
    {
        const string sql = """
            SELECT `steamid64`, `name`, `total_seconds`
            FROM `pt_players`
            ORDER BY `total_seconds` DESC
            LIMIT @count
            """;

        await using var conn = await _db.OpenConnectionAsync();
        var rows = await conn.QueryAsync<(ulong steamid64, string name, long total_seconds)>(
            new CommandDefinition(sql, new { count }, cancellationToken: ct));

        var result = new List<PlaytimeEntry>();
        foreach (var row in rows)
            result.Add(new PlaytimeEntry
            {
                SteamId  = (SteamID)row.steamid64,
                Name     = row.name,
                Playtime = TimeSpan.FromSeconds(row.total_seconds),
            });

        return result;
    }

    public async Task<IReadOnlyList<PlaytimeEntry>> GetTopByServerAsync(int serverId, int count, CancellationToken ct = default)
    {
        const string sql = """
            SELECT p.`steamid64`, pl.`name`, p.`total_seconds`
            FROM `pt_playtime` p
            JOIN `pt_players` pl ON pl.`steamid64` = p.`steamid64`
            WHERE p.`server_id` = @serverId
            ORDER BY p.`total_seconds` DESC
            LIMIT @count
            """;

        await using var conn = await _db.OpenConnectionAsync();
        var rows = await conn.QueryAsync<(ulong steamid64, string name, long total_seconds)>(
            new CommandDefinition(sql, new { serverId, count }, cancellationToken: ct));

        var result = new List<PlaytimeEntry>();
        foreach (var row in rows)
            result.Add(new PlaytimeEntry
            {
                SteamId  = (SteamID)row.steamid64,
                Name     = row.name,
                Playtime = TimeSpan.FromSeconds(row.total_seconds),
                ServerId = serverId,
            });

        return result;
    }

    public async Task<IReadOnlyList<PlaytimeEntry>> GetTopByTeamAsync(CStrikeTeam team, int count, CancellationToken ct = default)
    {
        var column = TeamColumn(team);
        var sql = $"""
            SELECT `steamid64`, `name`, `{column}` AS total_seconds
            FROM `pt_players`
            ORDER BY `{column}` DESC
            LIMIT @count
            """;

        await using var conn = await _db.OpenConnectionAsync();
        var rows = await conn.QueryAsync<(ulong steamid64, string name, long total_seconds)>(
            new CommandDefinition(sql, new { count }, cancellationToken: ct));

        var result = new List<PlaytimeEntry>();
        foreach (var row in rows)
            result.Add(new PlaytimeEntry
            {
                SteamId  = (SteamID)row.steamid64,
                Name     = row.name,
                Playtime = TimeSpan.FromSeconds(row.total_seconds),
            });

        return result;
    }

    public async Task<int> UpsertServerAsync(string ip, ushort port, string hostname)
    {
        const string sql = """
            INSERT INTO `pt_servers` (`ip`, `port`, `hostname`, `first_seen`, `last_seen`)
            VALUES (@ip, @port, @hostname, UTC_TIMESTAMP(), UTC_TIMESTAMP())
            ON DUPLICATE KEY UPDATE
                `hostname`  = @hostname,
                `last_seen` = UTC_TIMESTAMP()
            """;

        const string selectSql = "SELECT `id` FROM `pt_servers` WHERE `ip` = @ip AND `port` = @port";

        await using var conn = await _db.OpenConnectionAsync();
        await conn.ExecuteAsync(sql, new { ip, port, hostname });
        return await conn.QueryFirstAsync<int>(selectSql, new { ip, port });
    }

    public async Task<IReadOnlyList<ServerInfo>> GetAllServersAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT `id`, `ip`, `port`, `hostname`, `first_seen`, `last_seen` FROM `pt_servers`";

        await using var conn = await _db.OpenConnectionAsync();
        var rows = await conn.QueryAsync<ServerRow>(
            new CommandDefinition(sql, cancellationToken: ct));

        var result = new List<ServerInfo>();
        foreach (var row in rows)
            result.Add(MapServerRow(row));

        return result;
    }

    public async Task<ServerInfo?> GetServerByIdAsync(int serverId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT `id`, `ip`, `port`, `hostname`, `first_seen`, `last_seen`
            FROM `pt_servers`
            WHERE `id` = @serverId
            """;

        await using var conn = await _db.OpenConnectionAsync();
        var row = await conn.QueryFirstOrDefaultAsync<ServerRow>(
            new CommandDefinition(sql, new { serverId }, cancellationToken: ct));

        return row is null ? null : MapServerRow(row);
    }

    public async Task<(long globalSeconds, long serverSeconds, long ctSeconds, long teSeconds, long specSeconds)>
        GetBaselineAsync(ulong steamId64, int serverId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                COALESCE(pl.`total_seconds`, 0) AS globalSeconds,
                COALESCE(pt.`total_seconds`, 0) AS serverSeconds,
                COALESCE(pl.`ct_seconds`,    0) AS ctSeconds,
                COALESCE(pl.`te_seconds`,    0) AS teSeconds,
                COALESCE(pl.`spec_seconds`,  0) AS specSeconds
            FROM (SELECT 1) dummy
            LEFT JOIN `pt_players` pl  ON pl.`steamid64` = @steamId64
            LEFT JOIN `pt_playtime` pt ON pt.`steamid64` = @steamId64 AND pt.`server_id` = @serverId
            """;

        await using var conn = await _db.OpenConnectionAsync();
        var row = await conn.QueryFirstOrDefaultAsync<BaselineRow>(
            new CommandDefinition(sql, new { steamId64, serverId }, cancellationToken: ct));

        if (row is null)
            return (0, 0, 0, 0, 0);

        return (row.globalSeconds, row.serverSeconds, row.ctSeconds, row.teSeconds, row.specSeconds);
    }

    private static string TeamColumn(CStrikeTeam team) => team switch
    {
        CStrikeTeam.CT          => "ct_seconds",
        CStrikeTeam.TE          => "te_seconds",
        CStrikeTeam.Spectator   => "spec_seconds",
        _                       => "total_seconds",
    };

    private static ServerInfo MapServerRow(ServerRow row) => new ServerInfo
    {
        Id           = row.id,
        Ip           = row.ip,
        Port         = row.port,
        Hostname     = row.hostname,
        FirstSeenUtc = new DateTimeOffset(DateTime.SpecifyKind(row.first_seen, DateTimeKind.Utc)),
        LastSeenUtc  = new DateTimeOffset(DateTime.SpecifyKind(row.last_seen,  DateTimeKind.Utc)),
    };

    private sealed class ServerRow
    {
        public int      id         { get; init; }
        public string   ip         { get; init; } = string.Empty;
        public ushort   port       { get; init; }
        public string   hostname   { get; init; } = string.Empty;
        public DateTime first_seen { get; init; }
        public DateTime last_seen  { get; init; }
    }

    private sealed class BaselineRow
    {
        public long globalSeconds { get; init; }
        public long serverSeconds { get; init; }
        public long ctSeconds     { get; init; }
        public long teSeconds     { get; init; }
        public long specSeconds   { get; init; }
    }
}
