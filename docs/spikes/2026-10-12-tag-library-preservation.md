# Spike: do tag libraries preserve untouched data?

**Date:** 12 October 2026 | **Phase:** 1 | **Result:** no; led to [ADR-0008](../adr/0008-own-id3v2-frame-editor.md)

## Question
ADR-0005 proposed TagLib# for writing tags, on the condition that a write preserves audio and all non-edited frames byte for byte ([REQ-088](../requirements.md#req-088)). Does it, and does the main alternative (ATL.NET)?

## Method
- Six real MP3s from the owner's collection (Beatport and Traxsource downloads, all ID3v2.4, cover art up to 2 MB, Beatport files with UFID and many TXXX frames, two files with ID3v1). Worked on local copies only.
- Each library changed a single field (label) and saved. A small independent parser then compared every frame (id, flags, body bytes), frame order, the audio bytes and the ID3v1 tag before and after.

## Results

| Effect of changing only the label | TagLib# 2.3.0 | ATL.NET 7.18.0 |
|---|---|---|
| Audio bytes identical | yes | yes |
| ID3v2 version kept | yes | yes |
| Frame order kept | yes | **no** |
| Untouched text frames byte-identical | **no**: terminating NUL stripped from every text frame | **no**: terminators added or removed |
| UFID (store track id) | **destroyed**: 15 bytes reduced to 1 | **changed**: encoding byte inserted |
| ID3v1 | **added** to 4 files that had none | kept |
| Year (TDRC "2026") | kept | **changed** to "2026-01-01" |
| Store comment (COMM "ID3v1 Comment") | terminator stripped | **converted** into a TXXX frame |
| TXXX descriptions | terminator stripped | **upper-cased** ("bpm" to "BPM") |
| Cover art (APIC) | kept | **flags changed** (data length indicator added) |

None of the sample files contained Serato GEOB or Traktor PRIV frames, but both libraries parse and re-render such frames as well, so the same class of change would apply to them.

## Conclusion
Both libraries re-render the entire tag from their own object model. Any frame they model imperfectly is changed or damaged, even when the user edited something else entirely. This cannot meet [REQ-088](../requirements.md#req-088) for data written by DJ software, so tag writing is done by CrateEdit's own frame-level editor, which copies every non-edited frame's raw bytes (ADR-0008).

## Verification of the replacement
The same six files were edited with CrateEdit's writer, all nine fields at once. All other frames, their order and the audio stayed byte-identical (`RealFileTests`, run locally with `CRATEEDIT_SAMPLES`).
