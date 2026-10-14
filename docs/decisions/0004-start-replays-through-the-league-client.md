# Start replays through the League client's local replay API

- Status: accepted
- Date: 2026-10-14
- Supersedes: [0002](0002-start-replays-through-the-game-executable.md)

## Context

Record 0002 started `Game\League of Legends.exe` directly with the
client's command line. On the coach's machine Windows refused with
"Access is denied", from the app and from PowerShell alike. The file's
ACL allows execution and the executable asks for no elevation; Vanguard
(`vgk` running) lets only Riot's own client start the game. 0002 had only
compared command lines and was never tried with a real start.

## Considered options

1. The running client's local API (`https://127.0.0.1:{port}`, port and
   password from `<install>\lockfile`), the calls its Watch button makes.
2. Ask the coach to copy files into the client's replay folder and press
   Watch in the client.

## Decision

Option 1. Endpoints and payloads were read from the client's own `/help`
description on 16.20 and tried by hand before the code was written:

1. `GET /lol-replays/v1/configuration`: logged in, not patching, no game
   running, replays enabled.
2. `GET /lol-replays/v1/rofls/path`: the client's replay folder. The
   replay is copied there as `PLATFORM-GAMEID.rofl` unless an identical
   file already is. A renamed file gets a stable 12-digit id starting with
   9, from its content; the client does not compare the id with the file
   (a file named with another game's id played, and a made-up id reached
   the state `watch`).
3. `POST /lol-replays/v1/rofls/scan`, then poll
   `GET /lol-replays/v1/metadata/{gameId}` until `watch`. If the client
   has no metadata (404), `POST /lol-replays/v2/metadata/{gameId}/create`
   registers it once.
4. `POST /lol-replays/v1/rofls/{gameId}/watch` with
   `{"componentType":"replay-button_match-history"}`.

Without a running client the app offers to start it the way the Start-menu
shortcut does (`RiotClientServices.exe --launch-product=league_of_legends
--launch-patchline=live`) and waits up to five minutes for the login.

The client's certificate is self-signed; it is accepted only for
`127.0.0.1`. The lockfile is opened with `FileShare.ReadWrite`, since the
client keeps it open for writing.

## Consequences

- Watching needs the League client running and logged in.
- Player replays end up as copies in the client's replay folder
  (`Documents\League of Legends\Replays`); the library lists each once.
- The API is undocumented by Riot and may change with a patch; the
  client's `/help` (POST, with the lockfile credentials) shows the current
  shape.
- A replay still plays only on the build that recorded it; the client
  reports `incompatible`.
