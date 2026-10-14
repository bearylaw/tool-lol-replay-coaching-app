# State

- **Version:** 0.1.0, unreleased. Library and launcher work (roadmap step 1).
- **Next action:** the coach decides on going public (queue); then the
  next roadmap step.
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
