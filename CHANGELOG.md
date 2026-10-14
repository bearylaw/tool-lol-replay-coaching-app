# Changelog

All notable changes to this project are documented here, by hand. The format
follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the
project uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Ctrl+Space and Shift+Space play and pause the replay too, next to Space.

## [1.0.0] - 2026-10-14

First release: one self-contained `RiftReview.exe` for Windows 10 and 11 (x64),
licensed GPL-3.0-or-later. It reads Riot's plain replay metadata, when packets
come and for whom, and Riot's local APIs; nothing decodes packets, reads the
screen or the game's memory, or types into the game (decision 0008).

### Added

- **Library:** your own replays from the League client's folder and the player
  replays you add (Add replays or drag and drop; copied, never moved,
  duplicates skipped), filters (all, mine, players, roster) and search over
  players, champions, file names and patch. Each card shows its first
  bookmarks.
- **Match view:** scoreboard with champion, level, spells, runes, K/D/A, CS per
  minute, gold, damage and items; team comparison; charts per metric; every
  stat Riot records; a timeline of deaths and level-ups for replays of any
  patch; notes per game.
- **Watch in League** hands the replay to the running League client (offers to
  start it, warns about another build). **Watch with overlay** adds Rift
  Review's control bar on top, or attaches to a replay already running.
- **Overlay timeline:** deaths per team, turret falls, dragons, barons, heralds
  and finished grub camps in the colour of the team that took them, and your
  bookmarks in their tag's colour; click or drag to seek, play and pause
  (also Space), ±10 s, 0.25× to 8×. Hovering a mark shows the fight around
  it: who killed whom (with the assists), who destroyed which turret, who
  took which objective.
- **Selected player:** click a champion in League and the timeline highlights
  their deaths, kills, assists and turret takedowns; ◀ Death / Death ▶ and
  ◀ Kill / Kill ▶ jump to each, ten seconds before it.
- **Scoreboards:** League's own while Tab is held or from one O to the next,
  with the bar sliding up above it. Rift Review's scoreboard (item gold, CS per
  minute, kills and towers per team) is an option, off by default.
- **Drawing:** pointer, pen, highlighter, line, arrow, circle, eraser, six
  colours and widths, undo (also right-click) and clear; Ctrl+Alt+D / Z / X
  while League has the keyboard.
- **Bookmarks:** Ctrl+Alt+B marks the moment with a note. Each keeps where
  League's camera looked, and what was drawn; a jump brings the view back,
  and a drawn one pauses at that moment with the drawing on top.
- **Tags:** Ctrl+Alt+1 to 9 bookmark the moment with a tag and no note. Tags
  colour the bookmarks and can be switched in the note box (Ctrl+1 to 9) and
  the Notes tab. Settings → Tags: name, colour, order (the shortcut), add and
  remove; starts with Mistake, Good play, Vision, Macro and Laning.
- **Notes tab:** every bookmark editable and taggable; **Copy for Discord**
  puts the game, both teams, the bookmarks grouped by tag and in time order,
  and the free notes on the clipboard as Markdown.
- **Share review / Import review:** a `.riftreview` file with a replay's
  bookmarks, drawings, camera views, tags and notes, which a player imports
  onto their own copy of the same replay (decision 0010).
- **Settings** for the interface and overlay size, League's own interface
  parts, drawing and bookmark behaviour; `RIFTREVIEW_HOME` keeps everything in
  one folder for a portable copy. `--version`; logs in
  `%LOCALAPPDATA%\RiftReview\logs`.

[Unreleased]: https://github.com/bearylaw/tool-lol-replay-coaching-app/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/bearylaw/tool-lol-replay-coaching-app/releases/tag/v1.0.0
