# ADR-0010: Safe write technique: temp file, recovery record, replace, verify

- **Status:** Proposed
- **Date:** 2026-10-12

## Context
Writing tags is the only operation that puts the user's files at risk. A file must never end up half-written, lost or silently wrong, even after a crash, a power loss or a disconnected drive ([REQ-088](../requirements.md#req-088), [REQ-089](../requirements.md#req-089), [REQ-092](../requirements.md#req-092)). CrateEdit must support local disks, USB drives and cloud-sync mounts such as Google Drive ([REQ-011](../requirements.md#req-011)), and keep its recovery data locally ([REQ-013](../requirements.md#req-013)) and small ([REQ-090](../requirements.md#req-090), [REQ-091](../requirements.md#req-091)).

The [safe-write spike](../spikes/2026-10-12-safe-write.md) showed:
- `File.Replace` behaves consistently on NTFS, FAT32 and Google Drive, and keeps the file's identity.
- Only NTFS and APFS make it crash-atomic; on FAT32 and Google Drive it is a sequence of renames.
- An in-place overwrite is never crash-safe.
- Files opened by other software can block any write.

The tag writer already produces a complete new file, copying the audio byte for byte ([ADR-0008](0008-own-id3v2-frame-editor.md)).

## Decision
Every file is written in five steps. Each step only starts after the previous one succeeded.

1. **Record recovery material locally.** Save the file's original bytes before and after the audio (for MP3: the ID3v2 tag and the ID3v1 tag, if any), together with hashes of the audio and of the whole file, in the app data folder, and record "write in progress" for this file. Together with the unchanged audio in either version of the file, this is enough to rebuild the original exactly.
2. **Write a temporary file next to the original.** It is named `.<name>.crateedit-tmp` in the same folder, so the final replace is a rename within one folder and one volume. The temp file is flushed to disk.
3. **Verify the temporary file before it replaces anything.** Re-read it from disk and check:
   - its tag parses;
   - every edited field has the intended value;
   - every other frame is unchanged;
   - the audio bytes are identical to the original's (compared by hash).
4. **Replace the original** with `File.Replace(temp, original)`. This keeps the creation date and attributes on Windows, and uses an atomic `rename` on macOS.
5. **Verify the result and clean up.** Re-read the original path and check that it is the verified new content. Only then delete the recovery material ([REQ-090](../requirements.md#req-090)). The exception is an apply run that was cancelled and has an unresolved keep/revert decision: there the recovery material is kept ([REQ-085](../requirements.md#req-085)).

**Failures:**
- **Before step 4:** delete the temp file. The original was never touched.
- **A file blocked by another program** (sharing violation) is a transient error. It is retried with a bound, then reported ([REQ-079](../requirements.md#req-079)).
- **After a crash:** at the next start, every recorded "write in progress" is reconciled ([REQ-092](../requirements.md#req-092)):

  | Situation found | Action |
  |---|---|
  | Original intact, temp present | Delete the temp; the edit stays staged |
  | Original has the verified new content | Finish: delete the temp and the recovery material |
  | Original missing, temp complete | Finish the rename |
  | Anything else | Rebuild the original from the recovery material and whichever version of the file is present, or keep the track in unresolved recovery and tell the user ([REQ-093](../requirements.md#req-093)) |

**Rejected as a write path:** in-place tag overwrite, even when the tag fits the padding. It is not crash-safe and saves little time.

## Alternatives considered
- **`File.Move(overwrite)` instead of `File.Replace`:** loses the creation date on every tested storage type. On Google Drive it even succeeds while the file is open, but that is not consistent across storage types.
- **Full backup copy of each file:** simple, but up to 20 MB per file in app data and slow on USB. The tag-only recovery material is enough because the audio is never changed.
- **Relying on atomic replace alone:** safe on NTFS and APFS only, not on FAT32, exFAT or Google Drive.

## Consequences
- **The original file is never modified in place.** At every moment there is a complete original or a complete, verified new file, plus recovery material that can rebuild the original.
- **Temporary files appear briefly** in the user's folders. Sync clients and DJ software watching the folder may notice them. The hidden-style name keeps them out of the way, and reconciliation removes leftovers.
- **Every write is read back twice** (temp and result), which adds a hash of the audio per file. That is negligible on SSD and acceptable on USB.
- **Recovery records and reconciliation need a durable store,** which will be SQLite in Phase 2 ([ADR-0004](0004-sqlite-local-state.md)). Until then, the implementation keeps them as files in the app data folder behind an interface.
- **Next slice:** implement steps 1 to 5 and the reconciliation table as `SafeFileWriter` in `CrateEdit.Core.Tags.Writing`, with fault-injection tests that abort after each step and check that reconciliation always ends with a complete original or a complete, verified new file.
