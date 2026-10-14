# Fill the timeline from the replay file and a capture in the client, without Riot's web API

- Status: accepted; the capture is superseded by [0008](0008-public-edition-reads-only-plain-data-and-timing.md) (removed 2026-10-14), the rest stands
- Date: 2026-10-14
- Supersedes the data sources of [0005](0005-build-the-viewer-on-a-patch-independent-timeline.md); its timeline approach stands.

## Context

0005 listed Riot's Match-V5 web API as the source of map positions. The
coach does not want a Riot developer key (personal keys need registration,
development keys expire daily). The research spike then showed that
positions can be read from the minimap while a replay plays in the client,
with the replay API switched on (`EnableReplayApi=1`, set with the coach's
consent).

## Considered options

1. Match-V5 web API with a key: one position per minute, no scrims.
2. No web API. Two local sources only: the replay file (any patch) and a
   capture while the replay plays in the coach's client at high speed:
   Live Client Data (items, levels, scores, events, dead or alive), replay
   API (time, speed, fog of war) and the minimap picture (positions).
3. Replay file only: no positions.

## Decision

Option 2, the coach's choice. All data comes from the coach's own machine;
nothing needs an account, a key or a request to Riot's servers apart from
the public Data Dragon icons.

## Consequences

- The capture runs once per replay, at 8× speed with the game window
  visible, and only while the replay's patch is installed. The app
  captures replays when they are added; a captured timeline plays on any
  patch afterwards.
- Positions come from image recognition: about ±100 game units, with gaps
  where icons overlap, tracked over time and marked uncertain.
- Without a capture, a replay still has the timeline from the file:
  deaths and level-ups of every champion with times.
- If Riot's 26.21 replay restriction stops the client from playing other
  players' replays, captures of those replays stop working; the file
  timeline does not depend on the client.
