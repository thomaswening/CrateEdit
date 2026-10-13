# Probes: which WAV tags does Rekordbox read?

**Date:** 13 October 2026 | **Phase:** 1 | **Result:** led to [ADR-0011](../adr/0011-wav-tagging.md), resolves [OPEN-D](../requirements.md#open-d)

## Question
WAV files can carry tags in two places: a LIST/INFO chunk and an embedded ID3v2 tag ("id3 " chunk). Which does Rekordbox, the owner's DJ software, read, and how does it decode text?

## Method
Probe files were made from a copy of the owner's only WAV file, which carries identical values in both a LIST/INFO and an "id3 " chunk after the audio. The audio was left byte-identical; only the tag chunks were changed. The owner imported each probe into Rekordbox and reported what it showed.

| Probe | INFO | id3 |
|---|---|---|
| 1 | title "PROBE INFO", album "PROBE INFO ALBUM", genre "Probe INFO Genre" | title "PROBE ID3", album, genre, BPM 123, key 8A, label "Probe Label" |
| 2 | none | title "PROBE 2 ID3 ONLY", album, genre, label, year |
| 3a | title "Ärzte Röyksopp Beyoncé", artist, album, as UTF-8 | none |
| 3b | the same, as Windows-1252 | none |

## Results

| Probe | Rekordbox showed |
|---|---|
| 1 | Title, album and genre from **INFO**. Label **empty**. BPM and key from Rekordbox's own analysis (key 10B instead of 8A) |
| 2 | Title = **the file name**. Artist, album and label **empty** |
| 3a | All values **correct** |
| 3b | **Garbled** |

## Findings
1. **Rekordbox reads only the INFO chunk** of WAV files. It ignores the "id3 " chunk completely, even when there is no INFO chunk.
2. **INFO text is read as UTF-8.**
3. **Label, BPM and key cannot reach Rekordbox through a WAV file:** INFO has no fields for them, and the "id3 " chunk is ignored. BPM and key are overwritten by Rekordbox's analysis anyway.
4. Other software, such as Serato and tag editors, reads the "id3 " chunk. It is therefore still the only place where all fields of a WAV can be stored.
