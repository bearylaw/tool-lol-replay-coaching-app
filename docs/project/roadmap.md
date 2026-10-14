# Roadmap

What Rift Review does next, in order. Done steps stay as a short record.

## Done (0.1.0, unreleased)

- [x] Library: your own replays and the ones players send, filters, search,
      roster, notes per game.
- [x] Match view: scoreboard, team comparison, charts, every stat Riot
      records, timeline of deaths and level-ups from the replay file.
- [x] Watch in League through the running client (decision 0004).
- [x] Overlay on League's replay (decision 0008): its own timeline with
      deaths, turret falls and bookmarks; scoreboard; drawing; timestamped
      notes; League's own interface for team frames, kill feed, objective
      timers and the camera and vision panel.
- [x] Bookmarks shown with their times in the replay list and the match's
      Notes tab.

## Next

- [ ] Drawings saved with a bookmark.
- [ ] Playlist of bookmarks for a team meeting.
- [ ] Automatic key moments (deaths, objectives) to review or skip.
- [ ] Player comparison across games.

## Hardening

- [ ] Test on more patches; `just verify-replays` after each patch.
- [ ] Riot's 26.21 replay restriction: check whether player replays still
      open (queue item 1).
