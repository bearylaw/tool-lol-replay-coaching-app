# Build the own viewer on a patch-independent match timeline

- Status: accepted; data sources superseded by [0006](0006-no-riot-web-api-capture-from-the-client.md)
- Date: 2026-10-14

## Context

The coach wants an own replay viewer that plays replays from several game
versions, not only the installed patch. The research spike
([rofl2-payload.md](../research/rofl2-payload.md)) found:

- The `.rofl` container and its block layer are fully readable on 16.20
  without any game code: per-unit, timestamped packets, not encrypted.
- Packet contents (positions, damage, items) are obfuscated per patch,
  and some packet ids change even between games of the same build. Public
  decoders run the patch's own `League of Legends.exe` code in an emulator;
  since about 16.10 the executable is packed, and the only decoder reported
  working on 16.19 uses memory captures. Riot's Terms of Service forbid
  reverse engineering the game and reading its memory; since 2026-10-06
  Riot also blocks game-memory access for unknown third-party apps.
- Without decoding contents, each replay's own end-of-game stats identify
  its death and level-up packets (calibration), so every champion's deaths
  and level-ups come with timestamps on any patch.
- Riot's Match-V5 timeline API gives, per match, one frame per minute with
  each player's map position, gold, XP, level and CS, and events such as
  kills (with position, killer, assisters), ward placements, item
  purchases, buildings and epic monsters. It needs an API key; custom
  games (scrims) are visible only to participants who sign in.
- The game's Live Client Data API answers during replay playback in the
  client (items, levels, scores, events for all ten players; no
  positions), but only for replays of the installed patch.
- Riot announced for patch 26.21 that players can only access replays of
  matches they played in, and that third-party apps lose replay downloads;
  a solution for coaches is promised, details unknown.

## Considered options

1. Decode packet contents per patch, for a minimap with continuous
   movement. Requires running or reverse engineering Riot's game code or
   reading memory: against Riot's terms, technically blocked on packed
   builds, and per-game id shuffles on top.
2. A viewer on an own, versioned **match timeline** file per replay, filled
   from sources that do not depend on the installed patch: the replay's
   metadata and calibrated container events (always), the Match-V5
   timeline (when an API key is set and the match is reachable), and a
   Live Client Data capture while the replay's patch is still installed
   (for scrims and custom games). Once filled, a timeline plays forever.
3. Stay with the stats view of version 0.1.0.

## Decision

Option 2. It meets the hard requirement, because the timeline format is
ours and no source needs the replay's patch at viewing time. It uses only
the replay file and Riot-supported APIs. The honest limit: positions are
one sample per minute plus exact positions at events (kills, wards,
objectives), not continuous movement. For coaching this still gives a
death map, ward map, objective timeline, gold and XP graphs, item timings
and per-minute rotations.

## Consequences

- A personal Riot API key is needed for positions; without it the viewer
  shows the timeline from the replay alone (deaths, level-ups, activity).
- Scrims in custom games have no Match-V5 data for the coach unless a
  participant signs in; the Live Client capture covers them, but only
  while their patch is installed, and at playback speed unless the replay
  API (`EnableReplayApi=1` in `game.cfg`) is switched on.
- Counting packets in the container reads the replay file only and runs
  no game code; no source says this is prohibited, none says it is
  allowed.
- Revisit if Riot publishes a replay or packet format, if 26.21 changes
  what the client may play, or if a supported API starts exposing
  positions.
