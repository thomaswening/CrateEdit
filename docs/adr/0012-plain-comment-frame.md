# ADR-0012: The comment field is the comment frame without a description

- **Status:** Accepted
- **Date:** 2026-10-13
- **Amends:** the comment rule in [ADR-0008](0008-own-id3v2-frame-editor.md)

## Context
ID3v2 files can hold several comment frames (`COMM`), each with a language and a description. [ADR-0008](0008-own-id3v2-frame-editor.md) chose which one is "the comment": the one without a description; if there was none, a store's comment ("ID3v1 Comment", written by Beatport); otherwise the first non-iTunes comment.

Manual tests with files edited by CrateEdit showed:

| Comment frame CrateEdit edited | Rekordbox | Serato | Traktor |
|---|---|---|---|
| The store's "ID3v1 Comment" (Beatport MP3s) | not shown | not shown | shown |
| A new frame without a description (WAV with no comment) | shown | shown | shown |

So Rekordbox and Serato only read the comment frame **without** a description. Editing the store comment changes a value that two of the three DJ applications never display.

## Decision
- **The comment field is the comment frame with an empty description.** CrateEdit reads only that frame and writes only that frame, creating it (language "eng") if it is missing.
- **Comments with a description are other software's data,** like the store duplicates in TXXX frames ([ADR-0008](0008-own-id3v2-frame-editor.md)): a store's "ID3v1 Comment", iTunes' "iTunNORM" and similar. They are neither shown nor changed.

## Consequences
- **What CrateEdit shows matches Rekordbox and Serato.** For a store download that has only a store comment, CrateEdit shows an empty comment, as Rekordbox does.
- **Editing the comment of such a file adds a second comment frame.** The store's frame keeps its bytes. Traktor, which reads any comment, may keep showing the store comment, depending on which frame it picks.
- **An existing ID3v1 tag is still updated** with the edited comment, as before.
- **Confirmed:** after the change, the edited comment of Beatport MP3s showed in Rekordbox and Serato (13 Oct 2026).
