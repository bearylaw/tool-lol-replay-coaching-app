# Rift Review

Windows desktop tool for a League of Legends coach: lists the coach's own
replays and the `.rofl` files players send, shows each match's scoreboard
and stats, and starts a replay in the installed League client. C# / .NET /
WPF, one self-contained `.exe`.

Engineering standards: [core-standards-docs](https://github.com/bearylaw/core-standards-docs),
checked out at `D:\git\core\standards-docs`. **Read its `guidelines/rules.md`
before the first edit**; look up the rest by task through its
`guidelines/README.md`. This file is only what is true of this project.

## Commands

| | |
|---|---|
| install | `just install`, then `lefthook install` once |
| dev | `just dev` |
| check | `just check`: build with analyzers, format check, tests. Green means done |
| test | `just test`; show only failures |
| build | `just build`: `dist/RiftReview.exe`, single self-contained file |
| real replays | `just verify-replays <folder>` after a League patch |

## Git

IMPORTANT: commit each finished logical step with `check` green, and push the
branch before reporting done. Unasked, every session. Force-push, tags,
releases and deploys only when asked. Detail: the standards' `guidelines/git.md`.

## Conventions that differ from defaults

- Projects are the architecture lint: `RiftReview.Core` references nothing,
  each feature project references only Core, `RiftReview.App` wires them.
  Never add a reference between feature projects.
- UI text lives in `src/RiftReview.App/Localization/Strings.resx`, used via
  `{loc:Text Key}` in XAML and `Loc.Get("Key")` in C#. A test fails on
  unknown keys.
- The `.rofl` format is documented in `docs/rofl-format.md`. Tests build
  synthetic replays; never commit a real `.rofl` (real player data).

## The coach decides

Look and name, scope of the own viewer, anything that costs money (signing
certificate, Riot API product key), publishing. Open questions go to
`docs/project/queue.md`; the agent decides the rest and records the
non-obvious in `docs/decisions/`.

## Never

- Never force-push, tag, publish or deploy unless asked in this session.
- Never edit `.env`, `packages.lock.json` by hand, or generated files.
- Never commit anything under `replays/` except its README, or any `.rofl`.
- Never write to the League install or the Riot Client's files, except:
  copying a replay into the client's replay folder, and adding
  `EnableReplayApi=1` to game.cfg after the coach confirms (backup first).
- Never decode packet contents, read game memory, inject input, or unpack,
  disassemble or run the game program (decisions 0006, 0008). Replays are
  read for Riot's plain metadata and for when packets come and for whom;
  nothing reads the screen; live data comes from Riot's local APIs.
- Never start `League of Legends.exe` directly: Vanguard refuses it. Replays
  go through the client API (docs/decisions/0004).
- Never stop or start a League process you did not start in this session;
  the coach may be in a game.

## Context

- Use the Explore subagent for codebase-wide searches; return a summary.
- Prefer `git diff --stat` and `git log --oneline -20`.
- Do not re-read a file already read this session unless it changed.
