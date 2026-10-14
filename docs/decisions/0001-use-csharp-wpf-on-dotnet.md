# Use C#, WPF and .NET 10 for a Windows-only desktop tool

- Status: accepted
- Date: 2026-10-14

## Context

The coach needs a Windows `.exe` that lists `.rofl` replays, shows match
stats and starts replays in the installed League client. League replays
only play on Windows here, so cross-platform support buys nothing. A later
version adds an own replay viewer with a map, a timeline and charts. The
.NET 10 SDK is installed on the development machine; Rust and pnpm are not,
and the standards forbid installing tools ad hoc.

## Considered options

1. C# with WPF on .NET 10: native Windows UI, one self-contained `.exe`
   through single-file publish, no extra installs.
2. Electron with TypeScript: easiest styling, but needs pnpm installed, a
   larger bundle (~150 MB) and the Electron hardening list.
3. Tauri with TypeScript: small bundle, but needs Rust and pnpm installed.

## Decision

Option 1, chosen by the user. The desktop-tool playbook names WPF for
Windows-only C#. WPF draws the later map and timeline with its own vector
graphics, and the parser and domain code are plain .NET libraries the
viewer reuses.

Layout follows the standards with C# conventions: `RiftReview.Core`
(shared model, ROFL reader, logging), one project per feature
(`Library`, `Launch`, `GameData`) referencing only Core, and
`RiftReview.App` as composition root. Project references are the
architecture lint: a feature cannot reference another feature by accident
without a visible csproj change. Tests sit next to each project as
`<Project>.Tests`, since a C# test framework cannot live inside the shipped
assembly. `just` holds the golden path, as `dotnet` has no script runner.

## Consequences

- One 62 MB self-contained `.exe`; the coach needs no .NET install.
- WPF styling is more work than CSS. The theme lives in one
  `Theme/Theme.xaml`.
- Windows only. Revisit if the coach ever needs macOS.
- `SelfContained` and `PublishSingleFile` apply only when publishing,
  because a test project cannot reference a self-contained executable.
