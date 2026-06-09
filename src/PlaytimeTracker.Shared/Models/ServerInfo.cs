using System;

namespace InsanityGaming.ModSharp.PlaytimeTracker.Shared;

public sealed class ServerInfo
{
    public required int Id { get; init; }
    public required string Ip { get; init; }
    public required ushort Port { get; init; }
    public required string Hostname { get; init; }
    public required DateTimeOffset FirstSeenUtc { get; init; }
    public required DateTimeOffset LastSeenUtc { get; init; }
}
