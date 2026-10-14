# Changelog

All notable changes to this project are documented here, by hand. The format
follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the
project uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Bookmark notes can be changed in a match's Notes tab, not only in the
  overlay.
- Copy for Discord in a match's Notes tab: the game, both teams, every
  bookmark in time order (`- **2:23** – note`) and the free notes, as
  Markdown on the clipboard.
- Replay library: your own replays from the League client's folder and the
  player replays you add, with filters (all, mine, players, roster) and
  search over players, champions, file names and patch.
- Import player replays by drag and drop or Add replays; files are copied,
  never moved, duplicates are skipped.
- Match view: scoreboard with champion, level, spells, runes, KDA, CS per
  minute, gold, damage and items; team comparison of kills, gold, damage,
  towers, inhibitors, dragons, voidgrubs, barons and vision; charts per
  metric; every stat Riot records; notes per game.
- Timeline tab: every champion's deaths and level-ups with game time, read
  from the replay file itself, for replays of any patch.
- Watch in League: hands a replay to the running League client, which
  starts it; offers to start the client when it is not running, and warns
  when the replay was recorded on another build.
- Watch with overlay: plays the replay in League with Rift Review's control
  bar on top: its own timeline (death marks, turret falls, bookmarks; click
  or drag to seek), play and pause (also Space), ±10 s and 0.25× to 8×.
  League's own replay interface keeps the team frames, kill feed,
  objective timers and the camera and vision panel (fog of war for blue,
  red or both); its scoreboard, time controls and chat are off. Attaches
  to the replay when it already runs; replays open in League's directed
  camera.
- Scoreboard in the overlay (Tab or its button): both teams with level,
  K/D/A, CS and CS per minute, items, ward score and item gold at the
  replay's current time, and per team the kills and towers so far.
- Drawing on the overlay: pointer (the tools stay open, clicks reach the
  game), pen, highlighter, line, arrow with a head that grows with it,
  circle, eraser; six colours, width; undo (also right-click) and clear;
  a large pencil cursor. Ctrl+Alt+D / Z / X work while the game has the
  keyboard; these keys are taken only while League is in front, and
  Ctrl+Shift stays League's.
- Bookmarks with notes: Ctrl+Alt+B (or the bookmark button) marks the
  moment and opens a note box; the bookmark list jumps to each one, edits
  or deletes it, and the timeline shows them as gold marks. The replay list
  shows the first three with their times and the match's Notes tab all.
- Turret marks on the overlay's timeline, in the colour of the team that
  lost the turret.
- Settings for the interface size and the overlay; `RIFTREVIEW_HOME` keeps
  settings, notes, bookmarks and caches in one folder (portable use).
- App icon; single self-contained `RiftReview.exe`, `--version`, logs in
  `%LOCALAPPDATA%\RiftReview\logs`.

