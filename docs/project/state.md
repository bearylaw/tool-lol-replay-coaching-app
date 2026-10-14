# State

- **Version:** 1.0.0, released 2026-10-14 on GitHub (public repo
  `bearylaw/tool-lol-replay-coaching-app`). A tag `vX.Y.Z` matching
  `<Version>` in Directory.Build.props builds and publishes the exe in CI
  (`.github/workflows/release.yml`); the notes are the CHANGELOG section.
- **Next action:** the coach tests quick tags and shared reviews live; then
  the next feature the coach picks.
- **Decision 2026-10-14:** the app reads only Riot's plain data, when
  packets come and for whom, and Riot's local APIs (0008).
- **Goal:** everything the League replay client offers and more, any
  patch, no Riot web API (decision 0006).
- **Coach decisions 2026-10-14:** milestone 1 approved; name and look stay;
  no code signing; no Riot web API key;
  `EnableReplayApi=1` is added to game.cfg only after asking, with a backup.
- **Any patch:** the app reads replays of any game version; the timeline is
  its own versioned format (decision 0005).
- **Watch in League** goes through the running client's API (decision
  0004); the game cannot be started directly while Vanguard runs.
- **Open with the coach:** [queue.md](queue.md).
- **Sample data:** real replays stay outside the repository (they hold real
  players' Riot IDs); `just verify-replays <folder>` reads a folder of them.
- **Last handoff:** [handoffs/2026-10-14.md](handoffs/2026-10-14.md).
