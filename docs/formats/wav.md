# WAV tags: a guide for the code

This guide explains how tags are stored in WAV files and how CrateEdit edits them. It is the background for `src/CrateEdit.Core/Tags/Riff/`, `WavTagReader` and `WavTagWriter`, and for [ADR-0011](../adr/0011-wav-tagging.md). For the ID3 tag inside a WAV, see the [ID3 guide](id3.md).

## 1. Chunks
A WAV file is a RIFF file: a 12-byte header followed by a sequence of **chunks**.

```
"RIFF" | size (4 bytes, little-endian) | "WAVE" | chunk | chunk | ...
```

Each chunk is a 4-character id, a body size (32 bits, little-endian), the body, and a **pad byte** if the size is odd, so the next chunk starts at an even offset:

```
id (4) | size (4) | body (size bytes) | pad (0 or 1 byte)
```

Like ID3 frames, chunks can be skipped by their size without understanding them. That is how CrateEdit keeps every chunk it does not edit byte for byte.

## 2. The owner's WAV, as an example

```
RIFF size=94,150,962 WAVE
├─ fmt    16 bytes          PCM, 2 channels, 44.1 kHz, 16 bit
├─ data   94,150,324 bytes  the audio
├─ LIST   194 bytes         type INFO: INAM, IART, IPRD, IGNR, ICMT, ICRD
└─ id3    391 bytes         an ID3v2.3 tag (+1 pad byte)
```

Other chunks seen in practice include `bext` (broadcast metadata), `cue ` and `smpl` (markers and loops), `acid` (Ableton), `JUNK` and `PAD ` (padding).

## 3. The two tag chunks

**LIST/INFO.** A `LIST` chunk whose body starts with `INFO` and contains small sub-chunks of text, each ending with a zero byte.

| INFO id | Field |
|---|---|
| `INAM` | Title |
| `IART` | Artist |
| `IPRD` | Album |
| `IGNR` | Genre |
| `ICMT` | Comment |
| `ICRD` | Year |

There is no INFO field for label, BPM or key, and no defined text encoding. **Rekordbox reads only this chunk, as UTF-8** ([probes](../spikes/2026-10-13-rekordbox-wav-probes.md)).

**id3.** A chunk with id `id3 ` (sometimes `ID3 `) whose body is a complete ID3v2 tag, exactly as at the start of an MP3. It can hold all fields and DJ-software data such as Serato GEOB frames. Rekordbox ignores it; other software reads it.

## 4. How CrateEdit edits a WAV
`WavTagWriter.Rewrite`:
1. **Read the structure.** `RiffFile.Read` reads the chunk headers and the bodies of all chunks except `data`, so the audio stays on disk. A file that cannot be parsed completely is refused.
2. **Compute the new tag chunks.**
   - INFO: replace, add or remove only the edited entries.
   - id3: apply only the edited fields with the ID3 frame editor.
   - A missing chunk is created with all current values.
3. **Write the new file.**
   - Chunks are written in their original order. The two tag chunks are rebuilt; every other chunk, including `data` with the audio, is copied byte for byte.
   - New tag chunks are appended at the end, so the audio does not move.
   - Finally, the RIFF size in the header is set.

An INFO chunk is copied unchanged if no INFO field was edited (e.g. only the label).

## 5. Layout for the safe write
For the safe write ([ADR-0010](../adr/0010-safe-write-technique.md)), a WAV is split into three parts:
- **Prefix:** everything up to the start of the `data` body.
- **Audio:** the `data` body.
- **Suffix:** everything after it, including the data chunk's pad byte and all following chunks.

A tag edit changes the prefix and suffix, never the audio. That lets a crashed write be undone from the recorded original prefix and suffix.
