using InsanityGaming.ModSharp.PlaytimeTracker.Shared;
using InsanityGaming.PlaytimeTracker.Data;
using Microsoft.Extensions.Configuration;
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
    private readonly IConfiguration _config;
    private readonly ILogger<ServerRegistry> _logger;

    public int ServerId { get; private set; }
    public ServerInfo? ServerInfo { get; private set; }

    public ServerRegistry(
        InterfaceBridge bridge,
        PlaytimeRepository repo,
        IConfiguration config,
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
        var overrideVal = _config["server:ipOverride"];
        if (!string.IsNullOrWhiteSpace(overrideVal))
            return overrideVal;

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
        var overrideVal = _config["server:portOverride"];
        if (!string.IsNullOrWhiteSpace(overrideVal) && ushort.TryParse(overrideVal, out var overridePort))
            return overridePort;

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
        var overrideVal = _config["server:hostnameOverride"];
        if (!string.IsNullOrWhiteSpace(overrideVal))
            return overrideVal;

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
