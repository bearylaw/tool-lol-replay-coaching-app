# Share reviews as files keyed by the game

- Status: accepted
- Date: 2026-10-14

## Context

The coach wants a player to see the coach's bookmarks and drawings on the
player's own copy of a replay. Rift Review has no server and no accounts,
and the public edition sends nothing anywhere. The two copies of a replay
usually have different file names (the client names them, players rename
them), but the same platform and game id in Riot's metadata.

## Considered options

1. A file the coach sends however they like (Discord, mail).
2. Text pasted through the clipboard.
3. A shared online store.

## Decision

A `.riftreview` file: JSON with a format name and version, the replay's key
(platform and game id, else file name, the same key the bookmark and note
stores use), its bookmarks with drawings and camera, the tags they use and
the free notes. Import merges instead of replacing:

- a bookmark with the same id is replaced, so sharing again brings changes;
- a tag the receiver has (same id, else same name) is used as theirs, a new
  one is added to their list;
- the shared notes are appended to the receiver's unless already there.

A review of a replay the receiver has not added yet is stored under its key
and shows once the replay is in the library.

## Consequences

Nothing leaves the machine except the file the coach chooses to send. The
file holds the notes as written, so a coach who writes about a player sends
that text with it. A clipboard or online variant can reuse the same format.
