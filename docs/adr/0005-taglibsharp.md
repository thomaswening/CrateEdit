# ADR-0005: TagLib# for tag reading and writing

- **Status:** Superseded by [ADR-0008](0008-own-id3v2-frame-editor.md) (the Phase 1 spike showed that TagLib# does not preserve untouched frames)
- **Date:** 2026-10-09

## Context
CrateEdit must read and write 9 logical fields in MP3 and WAV files (REQ-003, REQ-007) while preserving all other data, especially proprietary frames written by Rekordbox, Serato and Traktor (REQ-088). WAV tagging conventions vary between DJ applications (OPEN-D).

## Decision
Use **TagLib#** (`TagLibSharp`) for tag access, behind CrateEdit's own field-mapping layer so the library can be replaced if needed.

## Alternatives considered
- **Writing ID3/RIFF parsing ourselves:** full control, but a large amount of format-specific work and risk.
- **Other .NET tag libraries (e.g. ATL.NET):** viable alternatives; to be evaluated if TagLib# fails the preservation tests.

## Consequences
- Mature support for ID3v2 in MP3, and for ID3 and RIFF INFO in WAV.
- LGPL-2.1: compatible with GPL-3.0 when shipped as an unmodified, replaceable DLL.
- Acceptance depends on the Phase 1 spike proving that writes preserve audio and all non-edited frames byte for byte, and that DJ software reads the written WAV fields.
