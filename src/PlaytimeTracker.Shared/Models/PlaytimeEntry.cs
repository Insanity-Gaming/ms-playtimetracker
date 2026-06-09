using Sharp.Shared.Units;
using System;

namespace InsanityGaming.ModSharp.PlaytimeTracker.Shared;

public sealed class PlaytimeEntry
{
    public required SteamID SteamId { get; init; }
    public required string Name { get; init; }
    public required TimeSpan Playtime { get; init; }
    public int? ServerId { get; init; }
}
