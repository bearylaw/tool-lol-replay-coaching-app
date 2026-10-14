# ROFL2 game data: what is inside

Research spike for the own viewer (roadmap step 3), started 2026-10-14.
Evidence: five real replays of build 16.20.824.8524 from the coach's
machine, both server-downloaded replays (about 50 chunks of 30 s) and one
replay with 28,647 small chunks (recorded differently, same layout).
Real replays never enter the repository; the findings below are checked
against them with throwaway scripts outside the repository.

## Layer 1: records — fully understood

The game data runs from right after the version string to 256 bytes
before the metadata JSON. It is a sequence of records with a 17-byte
header, matching the independent description in ROFL-X
([ROFL_FORMAT.md](https://github.com/Toastaspiring/ROFL-X/blob/HEAD/docs/ROFL_FORMAT.md)):

| Offset | Size | Field |
|---|---|---|
| 0 | 4 | id (u32 LE): chunk or keyframe id |
| 4 | 1 | counter; for chunks `id + 1`, for keyframes the next chunk id (ROFL-X calls it a time-slot counter) |
| 5 | 3 | zero in every record seen |
| 8 | 1 | stream tag (ROFL-X reads bytes 5–8 as one u32 with the tag in its high byte) |
| 9 | 4 | uncompressed size |
| 13 | 4 | compressed size; 0 means the data is stored raw |
| 17 | n | data: one zstd frame, or raw bytes |

| Tag | Count | Meaning |
|---|---|---|
| 3 | 1, first | initial keyframe, 12 to 19 KB |
| 4 | 1, 17 bytes raw | start marker, one block (see layer 2) |
| 1 | one per chunk | game chunk; ids run 3 … `lastGameChunkId` from the metadata |
| 2 | one per two chunks | keyframe (full state); ids run 1 … `lastKeyFrameId` |

The records end exactly at the 256-byte trailer (probably an RSA
signature, unverified) in all five files. The chunks are **not
encrypted**, only zstd-compressed; ROFL before patch 14.9 used Blowfish
([lol-ob notes](https://github.com/robertabcd/lol-ob/wiki/ROFL-Container-Notes)).

## Layer 2: blocks — fully understood

Decompressed data is a stream of blocks, the format League's spectator
stream has used for years. A marker byte says how wide the next fields are:

| Marker bit | Set | Clear |
|---|---|---|
| 0x80 | time: u8, milliseconds since the previous block | time: f32 LE, seconds since game start |
| 0x10 | length: u8 | length: u32 LE |
| 0x40 | packet type: same as the previous block | packet type: u16 LE |
| 0x20 | parameter: u8 added to the previous parameter | parameter: u32 LE |

Field order: marker, time, length, packet type, parameter, then `length`
bytes of payload. Whether the u8 parameter delta is signed is unconfirmed
(ROFL-X says signed; unsigned parses all five files). Verified: every record of every file
parses to its last byte (1.2 to 2.8 million blocks per game), and the
largest block time equals `gameLength` from the metadata to 0.1 s.

- **Parameter** = network id of the unit the packet belongs to. The ten
  champions are ten consecutive ids (`0x400000ae` … `0x400000b7` in all
  five files), in the same order as the players in the metadata.
- **Packet type**: about 270 distinct types per game. The ids are
  shuffled per patch.

## Turrets from a destruction marker — works without decoding payloads (2026-10-14)

Structures (turrets, inhibitors, nexus) are units present from the first
seconds, with ids just below the champions'. A turret only gets packets
while it fights, so its last packet is not its death: an untouched turret
goes quiet too. A destroyed one gets one packet of a rare type (`0xe8` on
16.20) and then at most a stray packet or
two for a minute; nexus turrets rebuild and can fall twice; inhibitors do
not get it. The type is found per replay as the rarest one whose such
falls number exactly the turrets lost in the stats (`FRIENDLY_TURRET_LOST`,
the same for every player of a team). On the five replays the counts match,
and on sample 2 every fall matches League's event feed in time and team.

Whose turret: the ids are fixed map objects (low byte; the high bytes
sometimes differ). On 16.20 blue's (Order) are `88 8b 8c 8d 8e 94 9a a2 a3
a6 ab`, red's (Chaos) `92 93 96 97 98 9b a0 a1 a4 a5 a7`; every replay's
losses per team fit this table. Where the champions crowd at the moment is
the fallback when a patch renumbers the ids: right for most falls, wrong
when a fight happens elsewhere (red's top turret at 12:24 in sample 2
fell during a bot lane fight), so the table is preferred.

## Reward bursts: kills, assists, turret takedowns — works without decoding payloads (2026-10-14)

When a unit dies the server pays the champions it credits in that tick (identical block time), in
a fixed order. Only types, times, file order within the tick and addressees are used. Type ids
below are 16.20's and are found per replay by calibrating against the end-of-game stats; `0x477`
is missing entirely in one game, so nothing is hard-coded.

- **Kills and assists** (`KillDecoder`): the pair (reward `0x2bb`, mark `0x1b5`) whose killers
  best match `CHAMPIONS_KILLED`. In a tick with the mark, the champions that get the mark after a
  reward are candidates; per team the killer is the last (assisters are paid first). Assisters are
  the killer's team getting the assist type (calibrated against `ASSISTS`). The victim gets the mark
  before any reward, else it is the nearest enemy death within 1.5 s. Exact kills per player on all
  five 16.20 replays; assists exact but one.
- **Turret takedowns** (`TurretCredit`): in the fall's tick (`TurretDecoder`), the killer is the
  first attacker to get the killer type (calibrated against `TURRETS_KILLED`); takedowns are the
  attackers getting the participant type plus the killer (`TURRET_TAKEDOWNS`). No killer means
  minions took it. Exact in three games, off by up to 7 in the game without `0x477`.

## Epic monsters — works without decoding payloads (2026-10-14)

`ObjectiveDecoder`. A monster is a non-champion unit created with a burst of packets and many kinds
of them. Its death is the type a long-lived unit gets once, a fixed 4.0 s before its last packet
(`0x458`, found per replay). Units are created before they appear, so the map's timers give
windows: first dragon created 290–296 s, the next 284–297 s after the last died (elder tried
344–357 s, untested), grubs one camp created together at 470–477 s (neighbouring even ids; one
grub can show up early), herald 893–899 s, first baron 1176–1182 s, the next 330–345 s after the
last died. Later dragons and barons are the unit in the window most like the first one (Jaccard of
packet kinds ≥ 0.6). In the death tick the last hit is the first champion paid the killer type
(`DRAGON_KILLS`…`HORDE_KILLS`), the takedowns are its team paid the participant type
(`Missions_TakedownDragons`, `Missions_TakedownBaronsElderDragons`). Exact per team on all five
replays, steals included (the team is the last hitter's). There is no Atakhan on 16.20.

## Layer 3: other packet payloads — not decoded

Payloads are obfuscated per patch: fields are bit-packed and transformed,
for example the level-up packet carries 1 or 2 bytes whose values do not
follow the new level. Public decoders get the transforms by running the
patch's own deserializers from `League of Legends.exe` in an emulator;
since about 16.10 the executable is packed on disk, and the only decoder
reported working on 16.19 uses memory captures of a running game
([ROFL-X COMPATIBILITY.md](https://github.com/Toastaspiring/ROFL-X/blob/HEAD/docs/COMPATIBILITY.md),
[rofl-binary-feasibility-probe](https://github.com/Ghou133/rofl-binary-feasibility-probe)).
Riot's Terms of Service forbid reverse engineering and reading game
memory ([terms](https://www.riotgames.com/en/terms-of-service)). Decision
[0005](../decisions/0005-build-the-viewer-on-a-patch-independent-timeline.md)
and [0008](../decisions/0008-public-edition-reads-only-plain-data-and-timing.md)
leave this layer alone.

## Calibration from the metadata — works without decoding payloads

The end-of-game stats give exact per-player counts. Counting packets per
type and champion id identifies event packets for the patch automatically:

| Stat (per player) | Packet type (16.20) | Match (correlation over the ten players) |
|---|---|---|
| deaths | `0xa6` / `0x1a5` / `0x83` (death) and `0x9e` / `0x1eb` (respawn), see below | r 0.89 to 0.995 |
| level-ups (level − 1) | `0x3c9` in all five | r 0.904 to 1.000 |

**Some packet ids change between games of the same build** (deaths
above), others do not (level-ups). A fixed table per patch would therefore
break; calibrating each replay against its own stats does not. The replay
with small chunks matches best (level-ups exact); server replays miss a
few events, likely at chunk boundaries or after the last chunk.

**Deaths and respawns (corrected 2026-10-14).** Two groups of packet types
follow the death counts. Against the kill feed of a capture, `0xa6` /
`0x1a5` / `0x83` fire at the kill (median −0.05 s) and `0x9e` / `0x1eb`
3.5 to 47 s later: the **respawn**. Until then the timeline took whichever
matched best, often a respawn type, so its "death" times were late by the
respawn timer. The groups are told apart without knowing the ids: a
champion's own packets drop to about a tenth in the 5 s after a death and
rise two- to eightfold after a respawn. Each type misses a few events (up
to a fifth in one server replay) and sends a few false ones (`0x83`
sometimes 6 s late, `0x1eb` alone after a recall), so deaths are anchored
on respawns (`DeathCalibration`): a respawn needs both respawn types and
no movement in the 6 s before it (the dead do not move; a recall does);
its death is the last death packet 3 to 130 s before, else the champion's
last move. Deaths without a respawn need two death types to agree.
Result on the five replays: 49 of 50 champions' death counts exact, one
short by one; death times within 0.54 s of the kill feed (47 kills).

So for any patch, these are available from the replay alone: every
champion's deaths and level-ups with timestamps, plus activity per
champion over time. Kills, assists, spells and items did not match a
single packet type and need the payload layer.

## The game's Live Client Data API during replay playback — works

Tested 2026-10-14: a replay started through the client (decision 0004)
serves Riot's documented Live Client Data API on
`https://127.0.0.1:2999/liveclientdata/…`:

- `gamestats`: game time, mode, map.
- `allgamedata` / `playerlist`: for all ten players champion, Riot ID,
  team, role, level, items (id, name, slot), scores (kills, deaths,
  assists, creep score, ward score), runes, summoner spells, dead or
  alive, respawn timer.
- `eventdata`: the event feed (game start, minions spawning, and in a
  running game kills with killer and assisters, objectives).
- No map positions. `screenPositionCenter` is the champion on screen,
  only for champions in the camera view (others are float max).
- `activePlayer`: "Spectator mode doesn't currently support this feature".
- `/replay/playback` and `/replay/render` answer 404; they need
  `EnableReplayApi=1` in `Config/game.cfg`, not set on this machine.

Sampling this while a replay plays gives a patch-independent timeline of
items, levels, scores and events, but only while the replay's patch is the
installed one, and at playback speed unless the replay API is enabled.

## Replay API — what the overlay can set

Tested 2026-10-14 with `EnableReplayApi=1` in `Config/game.cfg` (the app
asks before adding it and keeps a backup in `%LOCALAPPDATA%\RiftReview\backup`).

- `/replay/playback`: read and set `time` (seek), `paused`, `speed`
  (0.25× to 8× used).
- `/replay/render`: `fogOfWar` on or off, and which parts of League's own
  interface show: `interfaceTimeline` (play controls and timeline),
  `interfaceReplay` (the camera and vision panel at the bottom left),
  `interfaceScoreboard`, `interfaceScore`, `interfaceFrames`,
  `interfaceMinimap`, `interfaceTarget`, `interfaceKillCallouts`,
  `interfaceChat`, `interfaceNeutralTimers`.
- **Whose vision the fog shows (blue, red, all) is not in the API**
  (16.20, checked against the game's own openapi description and by
  comparing every render, game and playback field before and after
  switching it). It is chosen only in League's panel at the bottom left,
  so the overlay leaves that panel visible.
