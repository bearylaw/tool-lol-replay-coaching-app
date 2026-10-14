# The app reads only Riot's plain data, packet timing and Riot's local APIs

- Status: accepted
- Date: 2026-10-14
- Deciders: the coach

## Context

Rift Review should be shareable with other coaches. Riot's Terms of
Service forbid reverse engineering the game, and Riot's developer policies
ask third-party tools to use what Riot provides.

## Decision

The app uses:

- the replay file's plain metadata (end-of-game stats, items, runes);
- when packets in the replay file come and for which unit, never what
  they contain (deaths and respawns, turret falls);
- Riot's local Replay API and Live Client Data API while League plays a
  replay, and Data Dragon;

The overlay adds only what League's own replay interface does not have
or does not show as well: its own timeline with deaths, turret falls and
bookmarks, drawing and timestamped notes. Team frames, kill feed,
objective timers and the camera and vision panel stay League's.

Nothing reads the screen: the capture (League's minimap read while a
replay played at 8×) was removed at the coach's request (2026-10-14),
which supersedes the capture part of decision 0006.

## Consequences

- Nothing depends on decoding a patch's packet contents; what the app
  reads survives patches, checked by `just verify-replays`.
- Packet timing is not something Riot documents; it decodes nothing, but
  before publishing, the coach asks Riot whether it is fine.
- A public repository starts from a fresh history of `main`.
