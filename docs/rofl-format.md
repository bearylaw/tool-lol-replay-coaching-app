# The `.rofl` replay format

What Rift Review relies on. Verified against six replays recorded on game
build 16.20.824.8524 (October 2026), and against the open-source reader
[roflxd.cs](https://github.com/fraxiinus/roflxd.cs) (`Rofl2Reader.cs`).

## ROFL2 layout

| Offset | Size | Content |
|---|---|---|
| 0 | 6 | Signature `RIOT 02 00`. `02` is the container version |
| 6 | 8 | Unknown; identical in every file seen (`14 2c ab d4 5e 45 d8 3f`) |
| 14 | 1 | Length *n* of the game version string |
| 15 | *n* | Game version, ASCII, for example `16.20.824.8524` |
| 15 + *n* | … | Game data: chunks and keyframes, zstd-compressed, not read |
| end − 4 − *m* | *m* | Metadata JSON, UTF-8 |
| end − 4 | 4 | *m*, the metadata length, unsigned 32-bit little endian |

roflxd reads the version as a fixed 14 bytes from offset 15; Rift Review
uses the length byte at 14 instead, which gives the same result for
14-character versions and stays correct for shorter ones such as `16.9.x.y`.

## Metadata JSON

```json
{"gameLength":1506188,"lastGameChunkId":42,"lastKeyFrameId":21,"statsJson":"[...]"}
```

- `gameLength`: milliseconds.
- `statsJson`: a JSON array *inside a string*, one object per player. Every
  value is a string, numbers included. About 300 keys per player; the set
  changes between patches (seasonal mission keys come and go). The keys the
  app types are in `src/RiftReview.Core/Rofl/PlayerStatsParser.cs`.
- `statsJson` is `"[]"` for very short games, seen on a 71-second private
  lobby game.

The older container (`RIOT 00 00`, a 256-byte signature, then offsets to
the metadata) is not read: those replays come from patches no current client
can play. They show as "unsupported format".

## Playing a replay

The game executable cannot be started by other programs while Vanguard
runs ("Access is denied"). The League client starts it; Rift Review asks
the client to, through its local replay API. The steps and endpoints are in
[decision 0004](decisions/0004-start-replays-through-the-league-client.md).
The client plays only files in its replay folder named
`PLATFORM-GAMEID.rofl`, and only on the game build that recorded them.

For reference, the command line the client used (from the game log
`Logs/GameLogs/<time>/<time>_r3dlog.txt`):

```text
"C:/Users/<user>/Documents/League of Legends/Replays/sample-4.rofl"
"-GameBaseDir=C:Riot GamesLeague of Legends" "-Region=EUW"
"-PlatformID=EUW1" "-Locale=en_GB" "-SkipBuild" "-EnableCrashpad=true"
```

## Checking a new patch

```bash
just verify-replays "C:\Users\<user>\Documents\League of Legends\Replays"
```

reads every replay in the folder and prints version, length, player count
and winner per file.
