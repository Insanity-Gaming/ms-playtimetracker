using Sharp.Shared.Units;
using Sharp.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace InsanityGaming.ModSharp.PlaytimeTracker.Shared;

public interface IPlaytimeTracker
{
    public const string Identity = "PlaytimeTracker";

    // ── This server's identity ──────────────────────────────────────────
    int CurrentServerId { get; }
    ServerInfo CurrentServer { get; }

    // ── Sync — ONLINE only, by PlayerSlot (in-memory, no DB) ───────────
    bool IsTracked(PlayerSlot slot);
    PlaytimeSession? GetCurrentSession(PlayerSlot slot);
    IReadOnlyList<PlaytimeSession> GetCurrentSessions();
    TimeSpan GetSessionPlaytime(PlayerSlot slot);
    TimeSpan GetServerPlaytime(PlayerSlot slot);
    TimeSpan GetGlobalPlaytime(PlayerSlot slot);
    TimeSpan GetTeamPlaytime(PlayerSlot slot, CStrikeTeam team);

    // ── Async — works OFFLINE, by SteamID, hits DB ──────────────────────
    Task<TimeSpan> GetTotalPlaytimeAsync(SteamID steamId, CancellationToken ct = default);
    Task<TimeSpan> GetPlaytimeOnServerAsync(SteamID steamId, int serverId, CancellationToken ct = default);
    Task<TimeSpan> GetTeamPlaytimeAsync(SteamID steamId, CStrikeTeam team, CancellationToken ct = default);
    Task<IReadOnlyList<PlaytimeEntry>> GetTopPlaytimeAsync(int count, CancellationToken ct = default);
    Task<IReadOnlyList<PlaytimeEntry>> GetTopPlaytimeOnServerAsync(int serverId, int count, CancellationToken ct = default);
    Task<IReadOnlyList<PlaytimeEntry>> GetTopPlaytimeByTeamAsync(CStrikeTeam team, int count, CancellationToken ct = default);

    // ── Server registry ─────────────────────────────────────────────────
    Task<IReadOnlyList<ServerInfo>> GetRegisteredServersAsync(CancellationToken ct = default);
    Task<ServerInfo?> GetServerInfoAsync(int serverId, CancellationToken ct = default);

    // ── Events ───────────────────────────────────────────────────────────
    event Action<SessionEventArgs> SessionStarted;
    event Action<SessionEventArgs> SessionEnded;
    event Action<PlaytimeUpdatedEventArgs> PlaytimeUpdated;
}
