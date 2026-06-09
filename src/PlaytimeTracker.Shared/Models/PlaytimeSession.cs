using Sharp.Shared.Units;
using Sharp.Shared.Enums;
using System;
using System.Collections.Generic;

namespace InsanityGaming.ModSharp.PlaytimeTracker.Shared;

public sealed class PlaytimeSession
{
    public required SteamID SteamId { get; init; }
    public required PlayerSlot Slot { get; init; }
    public required string Name { get; init; }
    public required int ServerId { get; init; }
    public required DateTimeOffset StartedUtc { get; init; }
    public DateTimeOffset LastSeenUtc { get; set; }
    public required CStrikeTeam CurrentTeam { get; set; }
    public TimeSpan Elapsed { get; set; }
    public TimeSpan CtPlaytime { get; set; }
    public TimeSpan TePlaytime { get; set; }
    public TimeSpan SpecPlaytime { get; set; }
    public bool Resumed { get; init; }
}
