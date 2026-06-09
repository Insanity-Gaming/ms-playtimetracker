using InsanityGaming.ModSharp.PlaytimeTracker.Shared;
using Microsoft.Extensions.Logging;
using Sharp.Shared.Definition;
using Sharp.Shared.Enums;
using Sharp.Shared.GameEntities;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InsanityGaming.PlaytimeTracker.Commands;

internal sealed class PlaytimeCommands : BaseCommand
{
    private static readonly string Prefix = $" {ChatColor.Gold}[PT]{ChatColor.White}";

    private readonly IPlaytimeTracker _tracker;

    public PlaytimeCommands(InterfaceBridge bridge, ILogger<PlaytimeCommands> logger, IPlaytimeTracker tracker)
        : base(bridge, logger)
    {
        _tracker = tracker;
    }

    public override bool Init()
    {
        RegisterCommand("playtime", OnPlaytime);
        RegisterCommand("pt",       OnPlaytime);
        RegisterCommand("session",  OnSession);
        RegisterCommand("ses",      OnSession);
        RegisterCommand("playtimetop", OnTop);
        RegisterCommand("ptop",        OnTop);
        return true;
    }

    // !playtime [server|ct|t|spec]
    private void OnPlaytime(IGameClient? client, StringCommand command)
    {
        if (client is null) return;
        var ctrl = client.GetPlayerController();
        if (ctrl is null) return;

        var arg = command.ArgCount > 1 ? command.GetArg(1).ToLower() : string.Empty;
        var ch  = command.ChatTrigger ? HudPrintChannel.Chat : HudPrintChannel.Console;

        switch (arg)
        {
            case "server":
            {
                var time = _tracker.GetServerPlaytime(client.Slot);
                ctrl.Print(ch, $"{Prefix} Server playtime: {ChatColor.Green}{FormatTime(time)}");
                break;
            }
            case "ct":
            {
                var time = _tracker.GetTeamPlaytime(client.Slot, CStrikeTeam.CT);
                ctrl.Print(ch, $"{Prefix} CT playtime: {ChatColor.Blue}{FormatTime(time)}");
                break;
            }
            case "t":
            {
                var time = _tracker.GetTeamPlaytime(client.Slot, CStrikeTeam.TE);
                ctrl.Print(ch, $"{Prefix} T playtime: {ChatColor.Yellow}{FormatTime(time)}");
                break;
            }
            case "spec":
            {
                var time = _tracker.GetTeamPlaytime(client.Slot, CStrikeTeam.Spectator);
                ctrl.Print(ch, $"{Prefix} Spec playtime: {ChatColor.Grey}{FormatTime(time)}");
                break;
            }
            default:
            {
                var global = _tracker.GetGlobalPlaytime(client.Slot);
                var server = _tracker.GetServerPlaytime(client.Slot);
                ctrl.Print(ch, $"{Prefix} Global: {ChatColor.Green}{FormatTime(global)}{ChatColor.White} | Server: {ChatColor.Green}{FormatTime(server)}");
                ctrl.Print(ch, $"{Prefix} Use {ChatColor.Gold}!playtime server{ChatColor.White} / {ChatColor.Gold}ct{ChatColor.White} / {ChatColor.Gold}t{ChatColor.White} / {ChatColor.Gold}spec{ChatColor.White} for details.");
                break;
            }
        }
    }

    // !session
    private void OnSession(IGameClient? client, StringCommand command)
    {
        if (client is null) return;
        var ctrl = client.GetPlayerController();
        if (ctrl is null) return;

        var ch      = command.ChatTrigger ? HudPrintChannel.Chat : HudPrintChannel.Console;
        var session = _tracker.GetCurrentSession(client.Slot);

        if (session is null)
        {
            ctrl.Print(ch, $"{Prefix} {ChatColor.LightRed}No active session.");
            return;
        }

        ctrl.Print(ch, $"{Prefix} Session: {ChatColor.Green}{FormatTime(session.Elapsed)}{ChatColor.White} | CT: {ChatColor.Blue}{FormatTime(session.CtPlaytime)}{ChatColor.White} | T: {ChatColor.Yellow}{FormatTime(session.TePlaytime)}{ChatColor.White} | Spec: {ChatColor.Grey}{FormatTime(session.SpecPlaytime)}");
    }

    // !ptop [server|ct|t|spec]
    private void OnTop(IGameClient? client, StringCommand command)
    {
        if (client is null) return;
        var ctrl = client.GetPlayerController();
        if (ctrl is null) return;

        var ch  = command.ChatTrigger ? HudPrintChannel.Chat : HudPrintChannel.Console;
        var arg = command.ArgCount > 1 ? command.GetArg(1).ToLower() : string.Empty;

        _ = Task.Run(async () =>
        {
            IReadOnlyList<PlaytimeEntry> entries;
            string label;

            switch (arg)
            {
                case "server":
                    entries = await _tracker.GetTopPlaytimeOnServerAsync(_tracker.CurrentServerId, 10);
                    label = "Server";
                    break;
                case "ct":
                    entries = await _tracker.GetTopPlaytimeByTeamAsync(CStrikeTeam.CT, 10);
                    label = "CT";
                    break;
                case "t":
                    entries = await _tracker.GetTopPlaytimeByTeamAsync(CStrikeTeam.TE, 10);
                    label = "T";
                    break;
                case "spec":
                    entries = await _tracker.GetTopPlaytimeByTeamAsync(CStrikeTeam.Spectator, 10);
                    label = "Spec";
                    break;
                default:
                    entries = await _tracker.GetTopPlaytimeAsync(10);
                    label = "Global";
                    break;
            }

            ctrl.Print(ch, $"{Prefix} {ChatColor.Gold}Top 10 {label} Playtime:");
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                ctrl.Print(ch, $" {ChatColor.Gold}{i + 1}. {ChatColor.White}{e.Name} {ChatColor.Grey}- {ChatColor.Green}{FormatTime(e.Playtime)}");
            }

            if (entries.Count == 0)
                ctrl.Print(ch, $" {ChatColor.Grey}No data yet.");
        });
    }

    private static string FormatTime(TimeSpan t)
    {
        if (t.TotalHours >= 1)
            return $"{(int)t.TotalHours}h {t.Minutes}m {t.Seconds}s";
        if (t.TotalMinutes >= 1)
            return $"{t.Minutes}m {t.Seconds}s";
        return $"{t.Seconds}s";
    }
}
