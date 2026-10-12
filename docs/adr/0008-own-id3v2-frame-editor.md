# ADR-0008: Own frame-level ID3v2 editor instead of a tag library

- **Status:** Proposed
- **Date:** 2026-10-12
- **Supersedes:** [ADR-0005](0005-taglibsharp.md)

## Context
Background on the byte layout of MP3 tags: [ID3 format guide](../formats/id3.md).

Data safety is CrateEdit's top quality goal. Editing a field must never alter data CrateEdit does not own, above all the cue points, beatgrids and overviews that Serato and Traktor store in GEOB and PRIV frames ([REQ-088](../requirements.md#req-088), [REQ-130](../requirements.md#req-130)).

The [tag library spike](../spikes/2026-10-12-tag-library-preservation.md) showed that TagLib# and ATL.NET both re-render the whole tag when saving. On real files this destroyed a UFID frame, added unwanted ID3v1 tags, rewrote year values, converted a comment into a different frame, reordered frames and changed bytes of almost every untouched frame.

## Decision
CrateEdit reads and writes ID3v2.3 and ID3v2.4 tags with its own small frame-level editor (`CrateEdit.Core.Tags`):

- The tag is parsed into frames kept as **raw bytes**. Only the frames of edited fields are rebuilt; all other frames are written back exactly as read, in their original order.
- **Refuse rather than guess:** a tag that is not fully understood is not written. This includes ID3v2.2, tag-level unsynchronisation, extended headers, footers, invalid frames, unrecognised data after the frames, edited frames that are compressed or encrypted, and duplicate frames of an edited field. The user is told why, and the file stays unchanged.
- **Layout:** if the new frames fit in the existing tag (using its padding), the tag keeps its size and the audio does not move. Otherwise the tag grows, with 4 KB of new padding for future edits.
- **Field mapping** (agreed with the owner):
  - Fields map to the standard frames: TPE1, TIT2, TALB, TCON, TPUB, TBPM, TKEY, COMM, and TDRC (v2.4) or TYER (v2.3).
  - Store duplicates such as `TXXX:label` or `TXXX:initial_key` are never touched.
  - **Comment:** the comment without a description is used; if there is none, the store comment ("ID3v1 Comment"); otherwise the first comment that is not a technical iTunes value. Its language and description are kept.
  - **ID3v1:** an existing ID3v1 tag is updated for title, artist, album, year, comment and genre (truncated, Latin-1). An ID3v1 tag is never added.
  - **Encoding:** an edited frame keeps its text encoding if that encoding can represent the new value; otherwise it switches to UTF-8 (v2.4) or UTF-16 (v2.3). Each frame's original terminator style is kept.
  - **Files without a tag** get a new ID3v2.3 tag, the version read by all major DJ software.
- Values are stored and returned as text exactly as written (e.g. BPM "128.00"). Several ID3v2.4 values in one frame are read as one comma-separated value.
- The writer never modifies its source file. It writes a complete new file; replacing the original safely is the job of the apply pipeline (Phase 1 safe-write slice).

## Alternatives considered
- **TagLib# ([ADR-0005](0005-taglibsharp.md)) or ATL.NET:** rejected because of the spike results.
- **A library for reading and own code for writing:** reading and writing would disagree on which frame holds a field (e.g. which comment), and edits would be based on values the writer did not read itself.
- **Patching a library to preserve raw frames:** a fork that would have to be maintained, and every library update would need re-verification.

## Consequences
- The guarantee "everything not edited stays byte-identical" is simple to state and to test. Independent fixture tests and real-file tests check it, and mutation testing showed they catch a library-style re-render.
- CrateEdit owns ID3 parsing code (a few hundred lines). Its scope is limited to text frames and comments; all other frames are opaque bytes.
- Files with unsupported tag features cannot be edited, and for now show no field values. A read-only fallback may be added if this occurs in practice.
- TagLib# is no longer a dependency, so its LGPL notice is not needed. Audio properties (duration, bitrate) for the table will need a separate, read-only solution in Phase 2.
- WAV ([OPEN-D](../requirements.md#open-d)) needs the same approach for RIFF chunks; ID3 handling is shared.
- Status is Proposed until the owner has reviewed it and real files touched by DJ software have been tested.
