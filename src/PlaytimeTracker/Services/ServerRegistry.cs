using InsanityGaming.ModSharp.PlaytimeTracker.Shared;
using InsanityGaming.PlaytimeTracker.Config;
using InsanityGaming.PlaytimeTracker.Data;
using Microsoft.Extensions.Logging;
using Sharp.Shared.Managers;
using Sharp.Shared.Objects;
using System;
using System.Threading.Tasks;

namespace InsanityGaming.PlaytimeTracker.Services;

public sealed class ServerRegistry
{
    private readonly InterfaceBridge _bridge;
    private readonly PlaytimeRepository _repo;
    private readonly ServerConfig _config;
    private readonly ILogger<ServerRegistry> _logger;

    public int ServerId { get; private set; }
    public ServerInfo? ServerInfo { get; private set; }

    public ServerRegistry(
        InterfaceBridge bridge,
        PlaytimeRepository repo,
        ServerConfig config,
        ILogger<ServerRegistry> logger)
    {
        _bridge = bridge;
        _repo   = repo;
        _config = config;
        _logger = logger;
    }

    public async Task RegisterAsync()
    {
        var ip       = ResolveIp();
        var port     = ResolvePort();
        var hostname = ResolveHostname();

        _logger.LogInformation(
            "PlaytimeTracker: registering server {Ip}:{Port} ({Hostname})", ip, port, hostname);

        var id   = await _repo.UpsertServerAsync(ip, port, hostname);
        var info = await _repo.GetServerByIdAsync(id);

        ServerId   = id;
        ServerInfo = info;

        _logger.LogInformation("PlaytimeTracker: registered as server id {ServerId}.", ServerId);
    }

    private string ResolveIp()
    {
        if (!string.IsNullOrWhiteSpace(_config.IpOverride))
            return _config.IpOverride;

        try
        {
            var val = _bridge.ConVarManager.FindConVar("ip")?.GetString();
            if (!string.IsNullOrWhiteSpace(val) && val != "0.0.0.0" && val != "127.0.0.1")
                return val;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PlaytimeTracker: failed to read 'ip' convar; using fallback.");
        }

        return "0.0.0.0";
    }

    private ushort ResolvePort()
    {
        if (_config.PortOverride is > 0 and <= 65535)
            return (ushort)_config.PortOverride.Value;

        try
        {
            var convar = _bridge.ConVarManager.FindConVar("hostport");
            if (convar is not null)
            {
                var raw = convar.GetUInt32();
                if (raw is > 0 and <= 65535)
                    return (ushort)raw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PlaytimeTracker: failed to read 'hostport' convar; using fallback.");
        }

        return 27015;
    }

    private string ResolveHostname()
    {
        if (!string.IsNullOrWhiteSpace(_config.HostnameOverride))
            return _config.HostnameOverride;

        try
        {
            var val = _bridge.ConVarManager.FindConVar("hostname")?.GetString();
            if (!string.IsNullOrWhiteSpace(val))
                return val;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PlaytimeTracker: failed to read 'hostname' convar; using fallback.");
        }

        return "Unknown";
    }
}
