# Start replays by running the game executable like the client does

- Status: superseded by [0004](0004-start-replays-through-the-league-client.md)
- Date: 2026-10-14

## Context

"Open in League" must play a `.rofl` from any folder, including files
players send. The League client plays replays only from its own replay
folder and only through its UI. The game log
(`Logs/GameLogs/<time>/<time>_r3dlog.txt`) shows the command line the
client uses to start a replay.

## Considered options

1. Run `Game\League of Legends.exe` with the client's arguments
   (replay path, `-GameBaseDir`, `-Region`, `-PlatformID`, `-Locale`,
   `-SkipBuild`, `-EnableCrashpad=true`), working directory `Game\`.
2. Copy the file into the client's replay folder and ask the client to play
   it through its local API (LCU `/lol-replays/v1/rofls/{gameId}/watch`):
   needs the client running and logged in, a lockfile token, and the
   client's file name convention.
3. Only open the replay folder and let the coach click Watch in the client.

## Decision

Option 1. It needs no running client and no login, works for any file
location, and reproduces the logged command line byte for byte (tested
against the log of a replay the client started on this machine). Region,
locale and install folder come from the Riot Client's own settings files;
the platform comes from the replay's file name, else from the region.

## Consequences

- The app writes nothing into the League install or the client's folders.
- If Riot changes the arguments, launching breaks until the command is
  updated; the game log shows the new command line.
- A replay plays only on the build that recorded it. The app compares the
  replay's build with the game executable's file version and warns before
  starting a mismatch.
- A game that is already running blocks a second start; the app says so
  instead of starting one.
