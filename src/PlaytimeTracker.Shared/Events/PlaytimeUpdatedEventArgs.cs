using Sharp.Shared.Units;
using System;

namespace InsanityGaming.ModSharp.PlaytimeTracker.Shared;

public sealed class PlaytimeUpdatedEventArgs
{
    public required PlaytimeSession Session { get; init; }
    public required TimeSpan Delta { get; init; }
}
