# ADR-0011: WAV tagging: INFO for Rekordbox, id3 for everything

- **Status:** Proposed
- **Date:** 2026-10-13

## Context
CrateEdit must edit tags in WAV files ([REQ-003](../requirements.md#req-003)), consistently with MP3 ([REQ-006](../requirements.md#req-006)), without touching anything it does not own ([REQ-088](../requirements.md#req-088), [REQ-130](../requirements.md#req-130)). WAV files can carry tags in a LIST/INFO chunk, an "id3 " chunk, or both. INFO holds title, artist, album, genre, comment and year, but no label, BPM or key, and it has no defined text encoding.

[Probes in Rekordbox](../spikes/2026-10-13-rekordbox-wav-probes.md) showed:
- Rekordbox reads **only INFO**, decoded **as UTF-8**.
- Rekordbox ignores the "id3 " chunk entirely.

Other software reads the "id3 " chunk. The owner's only WAV has both chunks with identical values, after the audio.

## Decision
- **Both chunks are written** for every edit:
  - **INFO** gets the edited fields it can hold, as UTF-8 with a terminating zero byte. That is what Rekordbox shows.
  - **"id3 "** gets every edited field, with the same frame-level editor as MP3 ([ADR-0008](0008-own-id3v2-frame-editor.md)).
- **A missing tag chunk is created** at the end of the file, after the audio, so the audio never moves. It is filled with **all current values**, not only the edited field, so Rekordbox or other software see complete tags.
- **Only edited entries are rebuilt in existing chunks.** All other INFO entries (e.g. ISFT), ID3 frames (e.g. Serato GEOB) and chunks (fmt, data, bext, cue, unknown ones) keep their bytes and order. An INFO chunk is not touched at all when only label, BPM or key are edited.
- **Reading follows what Rekordbox shows** (owner's choice): INFO first for title, artist, album, genre, comment and year; the "id3 " chunk for label, BPM and key, and as fallback when an INFO field is empty. INFO text is read as UTF-8, with a Windows-1252 fallback for older files.
- **Refusals** follow [REQ-130](../requirements.md#req-130). A file is not written if:
  - it is RF64 (over 4 GB);
  - a chunk is truncated or its size does not match the file;
  - it has bytes after the last chunk;
  - it has more or fewer than one data chunk;
  - it has more than one INFO or "id3 " chunk;
  - its INFO or ID3 content cannot be parsed completely.
- **For the safe write** ([ADR-0010](0010-safe-write-technique.md)), the audio is the body of the data chunk. Everything before it is the prefix; everything after it is the suffix.

## Alternatives considered
- **"id3 " chunk only:** Rekordbox would never show edits to WAV files.
- **INFO only:** label, BPM and key could not be stored, and other software reading "id3 " would show stale values.
- **Reading "id3 " first:** CrateEdit could show a different title than Rekordbox for the same file.
- **INFO as Windows-1252:** Rekordbox shows non-ASCII characters garbled (probe 3b).

## Consequences
- **Edits to label, BPM and key of WAV files are not visible in Rekordbox.** The UI should say so ([REQ-131](../requirements.md#req-131)).
- **Two chunks hold overlapping values.** Software that updates only one of them leaves them out of sync. CrateEdit shows the INFO value, as Rekordbox does, and the next edit of that field brings both back in line.
- **Creating a missing chunk writes values the user did not edit.** These are the values already shown, so nothing visible changes in CrateEdit.
- **Status stays Proposed** until the manual check in Rekordbox with an edited WAV passes ([manual verification](../manual-verification.md)).
