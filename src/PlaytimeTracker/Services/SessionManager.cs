using InsanityGaming.ModSharp.PlaytimeTracker.Shared;
using InsanityGaming.PlaytimeTracker.Config;
using InsanityGaming.PlaytimeTracker.Data;
using InsanityGaming.PlaytimeTracker.Interfaces;
using Microsoft.Extensions.Logging;
using Sharp.Shared.Enums;
using Sharp.Shared.Listeners;
using Sharp.Shared.Objects;
using Sharp.Shared.Units;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace InsanityGaming.PlaytimeTracker.Services;

public sealed class SessionManager : IModule, IClientListener
{
    private readonly InterfaceBridge _bridge;
    private readonly PlaytimeRepository _repo;
    private readonly ServerRegistry _registry;
    private readonly TrackingConfig _config;
    private readonly ILogger<SessionManager> _logger;

    private readonly Dictionary<PlayerSlot, LiveSession> _activeSessions = new();
    private readonly Dictionary<ulong, GraceSession> _graceCache = new();
    private Timer? _flushTimer;

    private int FlushIntervalSeconds  => _config.FlushIntervalSeconds;
    private int ReconnectGraceSeconds => _config.ReconnectGraceSeconds;

    // IClientListener
    int IClientListener.ListenerVersion  => IClientListener.ApiVersion;
    int IClientListener.ListenerPriority => 0;

    public event Action<SessionEventArgs>?        SessionStarted;
    public event Action<SessionEventArgs>?        SessionEnded;
    public event Action<PlaytimeUpdatedEventArgs>? PlaytimeUpdated;

    public SessionManager(
        InterfaceBridge bridge,
        PlaytimeRepository repo,
        ServerRegistry registry,
        TrackingConfig config,
        ILogger<SessionManager> logger)
    {
        _bridge   = bridge;
        _repo     = repo;
        _registry = registry;
        _config   = config;
        _logger   = logger;
    }

    // ── Lifecycle (called by the module entry class) ──────────────────────

    public bool Init()
    {
        _bridge.ClientManager.InstallClientListener(this);

        var interval = TimeSpan.FromSeconds(FlushIntervalSeconds);
        _flushTimer = new Timer(OnFlushTick, null, interval, interval);

        _logger.LogInformation(
            "PlaytimeTracker: SessionManager started (flush every {Interval}s).", FlushIntervalSeconds);
        return true;
    }

    public void Shutdown()
    {
        _flushTimer?.Dispose();
        _flushTimer = null;

        _bridge.ClientManager.RemoveClientListener(this);

        var now = DateTimeOffset.UtcNow;

        // Finalize all active sessions.
        foreach (var (slot, live) in _activeSessions)
        {
            try
            {
                FlushLiveSession(live, now);
                _ = _repo.FinalizeSessionAsync(
                    live.SessionDbId,
                    now.UtcDateTime,
                    (int)live.Public.Elapsed.TotalSeconds,
                    "shutdown");
                SessionEnded?.Invoke(new SessionEventArgs { Session = live.Public });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "PlaytimeTracker: error finalizing session for slot {Slot} during shutdown.", slot);
            }
        }
        _activeSessions.Clear();

        // Finalize all grace sessions.
        foreach (var (steamId64, grace) in _graceCache)
        {
            try
            {
                _ = _repo.FinalizeSessionAsync(
                    grace.SessionDbId,
                    now.UtcDateTime,
                    (int)grace.Public.Elapsed.TotalSeconds,
                    "shutdown");
                SessionEnded?.Invoke(new SessionEventArgs { Session = grace.Public });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "PlaytimeTracker: error finalizing grace session for {SteamId} during shutdown.", steamId64);
            }
        }
        _graceCache.Clear();

        _logger.LogInformation("PlaytimeTracker: SessionManager shut down.");
    }

    // ── IClientListener ───────────────────────────────────────────────────

    public void OnClientPutInServer(IGameClient client) { }

    public void OnClientPostAdminCheck(IGameClient client)
    {
        if (client.IsFakeClient)
            return;

        var slot      = client.Slot;
        var steamId64 = client.SteamId.AsPrimitive();

        _bridge.ModSharp.PushTimer(() =>
        {
            var current = _bridge.ClientManager.GetGameClient(slot);
            if (current is null || current.SteamId.AsPrimitive() != steamId64)
                return;
            _ = OnPlayerConnectAsync(current);
        }, 2.0);
    }

    public void OnClientDisconnecting(IGameClient client, NetworkDisconnectionReason reason)
    {
        OnPlayerDisconnect(client, reason.ToString());
    }

    // ── Player lifecycle ──────────────────────────────────────────────────

    private async Task OnPlayerConnectAsync(IGameClient client)
    {
        var steamId64 = client.SteamId.AsPrimitive();
        var slot      = client.Slot;
        var name      = client.Name;
        var now       = DateTimeOffset.UtcNow;
        var serverId  = _registry.ServerId;

        // Load DB baselines.
        long globalSec, serverSec, ctSec, teSec, specSec;
        try
        {
            (globalSec, serverSec, ctSec, teSec, specSec) =
                await _repo.GetBaselineAsync(steamId64, serverId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "PlaytimeTracker: failed to load baseline for {SteamId}.", steamId64);
            (globalSec, serverSec, ctSec, teSec, specSec) = (0, 0, 0, 0, 0);
        }

        // Check grace cache.
        if (_graceCache.TryGetValue(steamId64, out var grace))
        {
            var graceCutoff = grace.DisconnectedAt.AddSeconds(ReconnectGraceSeconds);
            if (now <= graceCutoff)
            {
                _graceCache.Remove(steamId64);

                var resumedSession = new PlaytimeSession
                {
                    SteamId      = client.SteamId,
                    Slot         = slot,
                    Name         = name,
                    ServerId     = serverId,
                    StartedUtc   = grace.Public.StartedUtc,
                    LastSeenUtc  = now,
                    CurrentTeam  = CStrikeTeam.UnAssigned,
                    Elapsed      = grace.Public.Elapsed,
                    CtPlaytime   = grace.Public.CtPlaytime,
                    TePlaytime   = grace.Public.TePlaytime,
                    SpecPlaytime = grace.Public.SpecPlaytime,
                    Resumed      = true,
                };

                var live = new LiveSession(resumedSession, now)
                {
                    SessionDbId     = grace.SessionDbId,
                    BaselineGlobal  = grace.BaselineGlobal,
                    BaselineServer  = grace.BaselineServer,
                    BaselineCt      = grace.BaselineCt,
                    BaselineTe      = grace.BaselineTe,
                    BaselineSpec    = grace.BaselineSpec,
                    LastFlushedCt   = grace.Public.CtPlaytime,
                    LastFlushedTe   = grace.Public.TePlaytime,
                    LastFlushedSpec = grace.Public.SpecPlaytime,
                };

                _activeSessions[slot] = live;
                _logger.LogInformation(
                    "PlaytimeTracker: resumed session for {Name} ({SteamId}).", name, steamId64);
                SessionStarted?.Invoke(new SessionEventArgs { Session = resumedSession });
                return;
            }

            // Grace expired — drop stale entry; will be finalized on next flush tick eviction.
            _graceCache.Remove(steamId64);
        }

        // Fresh session.
        long sessionDbId;
        try
        {
            sessionDbId = await _repo.InsertSessionAsync(steamId64, serverId, now.UtcDateTime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "PlaytimeTracker: failed to insert session row for {SteamId}.", steamId64);
            sessionDbId = 0;
        }

        var currentTeam = client.GetPlayerController()?.Team ?? CStrikeTeam.UnAssigned;

        var session = new PlaytimeSession
        {
            SteamId      = client.SteamId,
            Slot         = slot,
            Name         = name,
            ServerId     = serverId,
            StartedUtc   = now,
            LastSeenUtc  = now,
            CurrentTeam  = currentTeam,
            Elapsed      = TimeSpan.Zero,
            CtPlaytime   = TimeSpan.Zero,
            TePlaytime   = TimeSpan.Zero,
            SpecPlaytime = TimeSpan.Zero,
            Resumed      = false,
        };

        var newLive = new LiveSession(session, now)
        {
            SessionDbId    = sessionDbId,
            BaselineGlobal = TimeSpan.FromSeconds(globalSec),
            BaselineServer = TimeSpan.FromSeconds(serverSec),
            BaselineCt     = TimeSpan.FromSeconds(ctSec),
            BaselineTe     = TimeSpan.FromSeconds(teSec),
            BaselineSpec   = TimeSpan.FromSeconds(specSec),
        };

        _activeSessions[slot] = newLive;
        _logger.LogInformation(
            "PlaytimeTracker: started session for {Name} ({SteamId}).", name, steamId64);
        SessionStarted?.Invoke(new SessionEventArgs { Session = session });
    }

    private void OnPlayerDisconnect(IGameClient client, string reason)
    {
        var slot = client.Slot;
        if (!_activeSessions.TryGetValue(slot, out var live))
            return;

        var now = DateTimeOffset.UtcNow;

        try
        {
            var delta = FlushLiveSession(live, now);
            _ = PersistFlushAsync(live, delta);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "PlaytimeTracker: error during disconnect flush for slot {Slot}.", slot);
        }

        _activeSessions.Remove(slot);

        var grace = new GraceSession(live.Public, live.SessionDbId, now)
        {
            BaselineGlobal = live.BaselineGlobal,
            BaselineServer = live.BaselineServer,
            BaselineCt     = live.BaselineCt,
            BaselineTe     = live.BaselineTe,
            BaselineSpec   = live.BaselineSpec,
        };

        _graceCache[live.Public.SteamId.AsPrimitive()] = grace;

        _logger.LogInformation(
            "PlaytimeTracker: {Name} ({SteamId}) disconnected ({Reason}); grace {GraceSeconds}s.",
            live.Public.Name, live.Public.SteamId.AsPrimitive(), reason, ReconnectGraceSeconds);
    }

    // ── Flush timer ───────────────────────────────────────────────────────

    private void OnFlushTick(object? state)
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var (slot, live) in _activeSessions)
        {
            try
            {
                var delta = FlushLiveSession(live, now);
                _ = PersistFlushAsync(live, delta);

                // Sync current team from the controller so the next flush interval
                // accumulates to the correct bucket even if player_team was unreliable.
                var actualTeam = _bridge.ClientManager.GetGameClient(slot)?.GetPlayerController()?.Team;
                if (actualTeam.HasValue)
                    live.Public.CurrentTeam = actualTeam.Value;

                PlaytimeUpdated?.Invoke(new PlaytimeUpdatedEventArgs
                {
                    Session = live.Public,
                    Delta   = delta,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "PlaytimeTracker: error flushing session for {Name}.", live.Public.Name);
            }
        }

        var expired = new List<ulong>();
        foreach (var (steamId64, grace) in _graceCache)
        {
            if ((now - grace.DisconnectedAt).TotalSeconds >= ReconnectGraceSeconds)
                expired.Add(steamId64);
        }

        foreach (var steamId64 in expired)
        {
            var grace = _graceCache[steamId64];
            _graceCache.Remove(steamId64);

            try
            {
                _ = _repo.FinalizeSessionAsync(
                    grace.SessionDbId,
                    now.UtcDateTime,
                    (int)grace.Public.Elapsed.TotalSeconds,
                    "timeout");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "PlaytimeTracker: error finalizing expired grace session for {SteamId}.", steamId64);
            }

            SessionEnded?.Invoke(new SessionEventArgs { Session = grace.Public });

            _logger.LogInformation(
                "PlaytimeTracker: grace session expired for {Name} ({SteamId}).",
                grace.Public.Name, steamId64);
        }
    }

    // Accumulates the current team-segment into buckets + Elapsed. Returns the segment delta.
    private TimeSpan FlushLiveSession(LiveSession live, DateTimeOffset now)
    {
        var segmentDelta = now - live.TeamSegmentStart;

        AccumulateTeamDelta(live.Public, live.Public.CurrentTeam, segmentDelta);
        live.Public.Elapsed    += segmentDelta;
        live.Public.LastSeenUtc = now;
        live.TeamSegmentStart   = now;
        live.LastFlushTime      = now;

        return segmentDelta;
    }

    private async Task PersistFlushAsync(LiveSession live, TimeSpan delta)
    {
        var s         = live.Public;
        var steamId64 = s.SteamId.AsPrimitive();
        var serverId  = s.ServerId;

        // Compute per-bucket increments since last flush.
        var ctDelta    = s.CtPlaytime   - live.LastFlushedCt;
        var teDelta    = s.TePlaytime   - live.LastFlushedTe;
        var specDelta  = s.SpecPlaytime - live.LastFlushedSpec;
        var totalDelta = ctDelta + teDelta + specDelta;

        live.LastFlushedCt   = s.CtPlaytime;
        live.LastFlushedTe   = s.TePlaytime;
        live.LastFlushedSpec = s.SpecPlaytime;

        var totalSec = (int)totalDelta.TotalSeconds;
        var ctSec    = (int)ctDelta.TotalSeconds;
        var teSec    = (int)teDelta.TotalSeconds;
        var specSec  = (int)specDelta.TotalSeconds;

        if (totalSec <= 0)
            return;

        await _repo.UpsertPlayerAsync(steamId64, s.Name, totalSec, ctSec, teSec, specSec);
        await _repo.UpsertServerPlaytimeAsync(steamId64, serverId, totalSec, ctSec, teSec, specSec);
    }

    private static void AccumulateTeamDelta(PlaytimeSession session, CStrikeTeam team, TimeSpan delta)
    {
        switch (team)
        {
            case CStrikeTeam.CT:
                session.CtPlaytime += delta;
                break;
            case CStrikeTeam.TE:
                session.TePlaytime += delta;
                break;
            case CStrikeTeam.Spectator:
            case CStrikeTeam.UnAssigned:
                session.SpecPlaytime += delta;
                break;
        }
    }

    // ── Sync read methods ─────────────────────────────────────────────────

    public PlaytimeSession? GetSession(PlayerSlot slot)
        => _activeSessions.TryGetValue(slot, out var live) ? live.Public : null;

    public IReadOnlyList<PlaytimeSession> GetAllSessions()
    {
        var result = new List<PlaytimeSession>(_activeSessions.Count);
        foreach (var live in _activeSessions.Values)
            result.Add(live.Public);
        return result;
    }

    public TimeSpan GetSessionPlaytime(PlayerSlot slot)
        => _activeSessions.TryGetValue(slot, out var live) ? live.Public.Elapsed : TimeSpan.Zero;

    public TimeSpan GetServerPlaytime(PlayerSlot slot)
    {
        if (!_activeSessions.TryGetValue(slot, out var live))
            return TimeSpan.Zero;
        return live.BaselineServer + live.Public.Elapsed;
    }

    public TimeSpan GetGlobalPlaytime(PlayerSlot slot)
    {
        if (!_activeSessions.TryGetValue(slot, out var live))
            return TimeSpan.Zero;
        return live.BaselineGlobal + live.Public.Elapsed;
    }

    public TimeSpan GetTeamPlaytime(PlayerSlot slot, CStrikeTeam team)
    {
        if (!_activeSessions.TryGetValue(slot, out var live))
            return TimeSpan.Zero;

        var sessionBucket = team switch
        {
            CStrikeTeam.CT        => live.Public.CtPlaytime,
            CStrikeTeam.TE        => live.Public.TePlaytime,
            CStrikeTeam.Spectator => live.Public.SpecPlaytime,
            _                     => TimeSpan.Zero,
        };

        var baseline = team switch
        {
            CStrikeTeam.CT        => live.BaselineCt,
            CStrikeTeam.TE        => live.BaselineTe,
            CStrikeTeam.Spectator => live.BaselineSpec,
            _                     => TimeSpan.Zero,
        };

        return baseline + sessionBucket;
    }

    // ── Inner types ───────────────────────────────────────────────────────

    private sealed class LiveSession
    {
        public PlaytimeSession Public           { get; }
        public long            SessionDbId      { get; set; }
        public DateTimeOffset  LastFlushTime    { get; set; }
        public DateTimeOffset  TeamSegmentStart { get; set; }

        // Per-bucket totals at the time of the last DB flush.
        public TimeSpan LastFlushedCt   { get; set; }
        public TimeSpan LastFlushedTe   { get; set; }
        public TimeSpan LastFlushedSpec { get; set; }

        // DB baselines loaded at connect time.
        public required TimeSpan BaselineGlobal { get; init; }
        public required TimeSpan BaselineServer { get; init; }
        public required TimeSpan BaselineCt     { get; init; }
        public required TimeSpan BaselineTe     { get; init; }
        public required TimeSpan BaselineSpec   { get; init; }

        public LiveSession(PlaytimeSession session, DateTimeOffset now)
        {
            Public           = session;
            LastFlushTime    = now;
            TeamSegmentStart = now;
        }
    }

    private sealed class GraceSession
    {
        public PlaytimeSession Public         { get; }
        public long            SessionDbId    { get; }
        public DateTimeOffset  DisconnectedAt { get; }

        public required TimeSpan BaselineGlobal { get; init; }
        public required TimeSpan BaselineServer { get; init; }
        public required TimeSpan BaselineCt     { get; init; }
        public required TimeSpan BaselineTe     { get; init; }
        public required TimeSpan BaselineSpec   { get; init; }

        public GraceSession(PlaytimeSession session, long sessionDbId, DateTimeOffset disconnectedAt)
        {
            Public         = session;
            SessionDbId    = sessionDbId;
            DisconnectedAt = disconnectedAt;
        }
    }
}
