namespace InsanityGaming.PlaytimeTracker.Config;

public sealed class PlaytimeTrackerConfig
{
    public DatabaseConfig Database { get; set; } = new();
    public TrackingConfig Tracking { get; set; } = new();
    public ServerConfig   Server   { get; set; } = new();
}

public sealed class DatabaseConfig
{
    public string Host     { get; set; } = "127.0.0.1";
    public int    Port     { get; set; } = 3306;
    public string Name     { get; set; } = "playtime";
    public string User     { get; set; } = "root";
    public string Password { get; set; } = "password";
}

public sealed class TrackingConfig
{
    public int FlushIntervalSeconds  { get; set; } = 60;
    public int ReconnectGraceSeconds { get; set; } = 600;
}

public sealed class ServerConfig
{
    public string? IpOverride       { get; set; }
    public int?    PortOverride     { get; set; }
    public string? HostnameOverride { get; set; }
}
