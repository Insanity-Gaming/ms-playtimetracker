# PlaytimeTracker

A [ModSharp](https://github.com/Kxnrl/modsharp-public) module for CS2 servers that tracks per-player playtime across all connected servers, broken down by team (CT, T, Spectator).

## Features

- Tracks total playtime and per-team time (CT, T, Spectator) for every player
- Per-server and global playtime records stored in MySQL
- Session-aware with a configurable reconnect grace period (players who rejoin within the window keep their session)
- Periodic flush to the database at a configurable interval
- Exposes a shared API (`PlaytimeTracker.Shared`) for other modules to read session data

## Commands

Commands can be used in chat with `!` / `.` prefix or in console directly. Aliases are interchangeable.

### `!playtime` / `!pt`

Shows your global and server playtime. Accepts an optional filter argument.

| Usage | Description |
|-------|-------------|
| `!playtime` | Global lifetime total + current server total |
| `!playtime server` | Current server playtime only |
| `!playtime ct` | CT team playtime (lifetime) |
| `!playtime t` | T team playtime (lifetime) |
| `!playtime spec` | Spectator playtime (lifetime) |

### `!session` / `!ses`

Shows your current active session broken down by team.

| Usage | Description |
|-------|-------------|
| `!session` | Session elapsed + CT / T / Spec split for this connection |

### `!playtimetop` / `!ptop`

Shows the top 10 players by playtime. Accepts an optional filter argument.

| Usage | Description |
|-------|-------------|
| `!ptop` | Global lifetime top 10 |
| `!ptop server` | Top 10 on the current server |
| `!ptop ct` | Top 10 by CT time |
| `!ptop t` | Top 10 by T time |
| `!ptop spec` | Top 10 by Spectator time |

---

## Requirements

- ModSharp 2.1.x
- MySQL / MariaDB
- .NET 10

## Installation

1. Download the latest release zip and extract it into your server's `game/sharp/` directory.
2. Copy `config.example.json` to `config.json` inside the module folder and fill in your database credentials.
3. The schema is applied automatically on first run — no manual migration needed.

## Configuration

`config.json`:

```json
{
  "database": {
    "host": "127.0.0.1",
    "port": 3306,
    "name": "playtime",
    "user": "user",
    "password": "pass"
  },
  "tracking": {
    "flushIntervalSeconds": 60,
    "reconnectGraceSeconds": 600
  },
  "server": {
    "ipOverride": null,
    "portOverride": null,
    "hostnameOverride": null
  }
}
```

| Key | Description |
|-----|-------------|
| `flushIntervalSeconds` | How often (in seconds) live session data is flushed to the database. |
| `reconnectGraceSeconds` | If a player reconnects within this window, their previous session is resumed instead of starting a new one. |
| `ipOverride` / `portOverride` / `hostnameOverride` | Override the auto-detected server identity stored in `pt_servers`. Useful behind NAT or load balancers. |

## Database Schema

Four tables are created automatically:

| Table | Purpose |
|-------|---------|
| `pt_servers` | One row per unique server (IP + port). |
| `pt_players` | Global lifetime totals per SteamID64. |
| `pt_playtime` | Per-player, per-server totals. |
| `pt_sessions` | Individual session records with start/end timestamps and duration. |

## Shared API

Other modules can reference `PlaytimeTracker.Shared` and consume `IPlaytimeTracker` via DI to read live session data and subscribe to events:

```csharp
public class MyModule : IModule
{
    private readonly IPlaytimeTracker _tracker;

    public MyModule(IPlaytimeTracker tracker) => _tracker = tracker;

    public bool Init()
    {
        _tracker.SessionStarted  += OnSessionStarted;
        _tracker.SessionEnded    += OnSessionEnded;
        _tracker.PlaytimeUpdated += OnPlaytimeUpdated;
        return true;
    }
}
```

## Building

```bash
dotnet build
dotnet publish -c Release
```

The release zip is produced by CI and includes the module DLL, dependencies, and `config.example.json`.

## License

MIT — see [LICENSE](LICENSE).
