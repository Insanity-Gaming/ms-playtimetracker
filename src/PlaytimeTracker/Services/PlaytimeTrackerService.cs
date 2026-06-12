using InsanityGaming.ModSharp.PlaytimeTracker.Shared;
using InsanityGaming.PlaytimeTracker.Data;
using Microsoft.Extensions.Logging;
using Sharp.Shared.Enums;
using Sharp.Shared.Units;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace InsanityGaming.PlaytimeTracker.Services;

public sealed class PlaytimeTrackerService : IPlaytimeTracker
{
    private readonly SessionManager _sessionManager;
    private readonly ServerRegistry _registry;
    private readonly PlaytimeRepository _repo;
    private readonly ILogger<PlaytimeTrackerService> _logger;

    public event Action<SessionEventArgs>? SessionStarted;
    public event Action<SessionEventArgs>? SessionEnded;
    public event Action<PlaytimeUpdatedEventArgs>? PlaytimeUpdated;

    // IPlaytimeTracker requires non-nullable event — satisfy via explicit interface.
    event Action<SessionEventArgs> IPlaytimeTracker.SessionStarted
    {
        add    => SessionStarted += value;
        remove => SessionStarted -= value;
    }

    event Action<SessionEventArgs> IPlaytimeTracker.SessionEnded
    {
        add    => SessionEnded += value;
        remove => SessionEnded -= value;
    }

    event Action<PlaytimeUpdatedEventArgs> IPlaytimeTracker.PlaytimeUpdated
    {
        add    => PlaytimeUpdated += value;
        remove => PlaytimeUpdated -= value;
    }

    public PlaytimeTrackerService(
        SessionManager sessionManager,
        ServerRegistry registry,
        PlaytimeRepository repo,
        ILogger<PlaytimeTrackerService> logger)
    {
        _sessionManager = sessionManager;
        _registry       = registry;
        _repo           = repo;
        _logger         = logger;

        _sessionManager.SessionStarted  += s => SessionStarted?.Invoke(s);
        _sessionManager.SessionEnded    += s => SessionEnded?.Invoke(s);
        _sessionManager.PlaytimeUpdated += a => PlaytimeUpdated?.Invoke(a);
    }

    // ── Server identity ──────────────────────────────────────────────────

    public int CurrentServerId => _registry.ServerId;

    public ServerInfo CurrentServer => _registry.ServerInfo!;

    // ── Sync — online players only ───────────────────────────────────────

    public bool IsTracked(PlayerSlot slot)
        => _sessionManager.GetSession(slot) is not null;

    public PlaytimeSession? GetCurrentSession(PlayerSlot slot)
        => _sessionManager.GetSession(slot);

    public IReadOnlyList<PlaytimeSession> GetCurrentSessions()
        => _sessionManager.GetAllSessions();

    public TimeSpan GetSessionPlaytime(PlayerSlot slot)
        => _sessionManager.GetSessionPlaytime(slot);

    public TimeSpan GetServerPlaytime(PlayerSlot slot)
        => _sessionManager.GetServerPlaytime(slot);

    public TimeSpan GetGlobalPlaytime(PlayerSlot slot)
        => _sessionManager.GetGlobalPlaytime(slot);

    public TimeSpan GetTeamPlaytime(PlayerSlot slot, CStrikeTeam team)
        => _sessionManager.GetTeamPlaytime(slot, team);

    // ── Async — hits DB, works for offline players ───────────────────────

    public Task<TimeSpan> GetTotalPlaytimeAsync(SteamID steamId, CancellationToken ct = default)
        => _repo.GetGlobalPlaytimeAsync(steamId.AsPrimitive(), ct);

    public Task<TimeSpan> GetPlaytimeOnServerAsync(SteamID steamId, int serverId, CancellationToken ct = default)
        => _repo.GetServerPlaytimeAsync(steamId.AsPrimitive(), serverId, ct);

    public Task<TimeSpan> GetTeamPlaytimeAsync(SteamID steamId, CStrikeTeam team, CancellationToken ct = default)
        => _repo.GetTeamPlaytimeGlobalAsync(steamId.AsPrimitive(), team, ct);

    public Task<IReadOnlyList<PlaytimeEntry>> GetTopPlaytimeAsync(int count, CancellationToken ct = default)
        => _repo.GetTopGlobalAsync(count, ct);

    public Task<IReadOnlyList<PlaytimeEntry>> GetTopPlaytimeOnServerAsync(int serverId, int count, CancellationToken ct = default)
        => _repo.GetTopByServerAsync(serverId, count, ct);

    public Task<IReadOnlyList<PlaytimeEntry>> GetTopPlaytimeOnServerByTeamAsync(int serverId, CStrikeTeam team, int count, CancellationToken ct = default)
        => _repo.GetTopByServerByTeamAsync(serverId, team, count, ct);

    public Task<IReadOnlyList<PlaytimeEntry>> GetTopPlaytimeByTeamAsync(CStrikeTeam team, int count, CancellationToken ct = default)
        => _repo.GetTopByTeamAsync(team, count, ct);

    // ── Server registry ──────────────────────────────────────────────────

    public Task<IReadOnlyList<ServerInfo>> GetRegisteredServersAsync(CancellationToken ct = default)
        => _repo.GetAllServersAsync(ct);

    public Task<ServerInfo?> GetServerInfoAsync(int serverId, CancellationToken ct = default)
        => _repo.GetServerByIdAsync(serverId, ct);
}
