# Fetch names and icons from Riot's Data Dragon and cache them on disk

- Status: accepted
- Date: 2026-10-14

## Context

Replays store champions, items, summoner spells and runes as internal
names and numeric ids. A scoreboard that looks like the game needs their
display names and icons. The game's own files hold them in WAD archives
whose format changes with patches.

## Considered options

1. Riot's public CDN, Data Dragon (`ddragon.leagueoflegends.com`), over
   HTTPS, cached in `%LOCALAPPDATA%\RiftReview\ddragon`.
2. Read the game's WAD archives.
3. Text only, no network.

## Decision

Option 1, chosen by the user. The app picks the Data Dragon version of the
replay's patch, downloads each file once and uses the cache afterwards, also
offline. It only fetches public files and sends no data. Every id and icon
path from the network is checked against a strict pattern before it becomes
a URL or a file name.

## Consequences

- The first view of a patch needs a connection; offline, names show instead
  of icons.
- The cache grows by a few MB per patch; deleting the folder is safe.
- Older replays get the icons of their own patch.
