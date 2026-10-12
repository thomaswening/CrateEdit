# Spike: replacing audio files safely

**Date:** 12 October 2026 | **Phase:** 1 | **Result:** led to [ADR-0010](../adr/0010-safe-write-technique.md), resolves [OPEN-C](../requirements.md#open-c)

## Question
The tag writer produces a complete new file and never modifies the original ([ADR-0008](../adr/0008-own-id3v2-frame-editor.md)). How should the original be replaced, so that a file is never left half-written or lost ([REQ-088](../requirements.md#req-088), [REQ-089](../requirements.md#req-089), [REQ-092](../requirements.md#req-092)), on every storage type CrateEdit supports ([REQ-011](../requirements.md#req-011))?

## Method
The spike program ([SafeWriteSpike.cs](2026-10-12-safe-write/SafeWriteSpike.cs)) replaced a copy of a 20 MB sample MP3 with each technique. For each technique it recorded:
- whether the replace succeeded and how long it took;
- whether the file kept its creation date;
- whether it still worked while another program had the file open for reading, both in the common Windows mode (`FileShare.Read`, which blocks writing and deleting) and in a permissive mode.

It ran on three storage types:
- the local SSD (NTFS);
- a USB stick (FAT32);
- the owner's Google Drive for desktop mount. This is a virtual drive, and it reports its file system as FAT32.

The test folders on the USB stick and on Google Drive were deleted afterwards; no conflict copies or leftover files appeared.

## Results

| | Local SSD (NTFS) | USB stick (FAT32) | Google Drive |
|---|---|---|---|
| **Temp file + `File.Replace`** | ok, creation date kept | ok, creation date kept | ok, creation date kept |
| ...while open by a reader with `FileShare.Read` | fails | fails | fails |
| ...while open by a permissive reader | ok | ok | ok |
| **Temp file + `File.Move(overwrite)`** | ok, creation date **lost** | ok, creation date **lost** | ok, creation date **lost** |
| ...while open by any reader | fails | fails | **ok** |
| **In-place tag overwrite** (if it fits the padding) | ok | ok | ok |
| ...while open by a reader with `FileShare.Read` | fails | fails | fails |
| Writing and flushing a 20 MB file | 12 ms | 856 ms | 58 ms (into Drive's local cache; uploaded later) |

## Findings
1. **`File.Replace` is the most consistent technique.** It works the same on all three storage types and keeps the file's identity: creation date and attributes. On macOS, .NET implements it with an atomic `rename`.
2. **Only NTFS (and APFS on macOS) make the replace crash-atomic.** On FAT32 and exFAT, and on Google Drive, which behaves like FAT32, a replace is a sequence of renames. A power loss at the wrong moment can leave the target name briefly missing. Safety therefore cannot rely on atomic replace alone.
3. **The in-place overwrite is never crash-safe.** A crash during the write leaves a half-written tag. It saves little time, since full writes are fast, so it is not worth a second write path.
4. **A file opened by other software blocks the write.** This happens on every storage type when a player or DJ application holds the file with Windows' common sharing mode. It is a transient failure: retry, then report ([REQ-079](../requirements.md#req-079)). CrateEdit's own player must open files permissively, so it never blocks its own writes (relevant to [OPEN-K](../requirements.md#open-k)).
5. **Speed is not a concern.** It ranges from about 1 second per 20 MB file on a slow USB stick to milliseconds on SSD and Google Drive (the Drive client uploads in the background).
6. **Recovery material can be small.** The writer copies the audio unchanged, so the original file can be rebuilt from the current file plus the **original tag bytes and original ID3v1**: kilobytes, or up to a few MB with large cover art, instead of a full 20 MB copy ([REQ-091](../requirements.md#req-091)).

## Not covered
- **macOS:** to be covered by integration tests running on the macOS CI runner.
- **Google Drive on the server side:** whether Drive keeps the replaced file as one file with version history or treats it as a new file. This does not affect the local result; it is listed as a manual check.
- **Google Drive with a lost connection during a write:** not reproducible in the spike. It is covered by the same reconciliation as a crash.
- **Power-loss testing:** this is simulated with fault injection in the implementation's tests rather than by pulling the plug.
