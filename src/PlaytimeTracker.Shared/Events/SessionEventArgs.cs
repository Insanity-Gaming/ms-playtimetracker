using System;

namespace InsanityGaming.ModSharp.PlaytimeTracker.Shared;

public sealed class SessionEventArgs
{
    public required PlaytimeSession Session { get; init; }
}
