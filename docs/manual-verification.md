# CrateEdit: Manual Verification

Requirements that cannot be verified by automated tests are checked by hand before a phase is considered complete and before each release. The traceability report counts a requirement listed here as **Manual** when no automated test covers it.

Add a row when a requirement needs a manual check; record the date and version each time it is verified.

| Requirement | Check | Last verified |
|---|---|---|
| [REQ-125](requirements.md#req-125) | With Windows or macOS set to a German region, dates and numbers in the UI use German formatting (e.g. `12.10.2026`, `1,5`). Only meaningful once the UI shows dates or numbers (Phase 2). | not yet |
| [REQ-088](requirements.md#req-088) | Edit all fields of copies of MP3s that Serato and/or Traktor have analyzed (with cue points set). Open them in that software again: cue points, beatgrid and waveform overview are unchanged. | 13 Oct 2026: passed. 3 MP3s and 1 WAV each in Serato (GEOB markers, beatgrid, overview) and Traktor (PRIV frames; NITR entry in WAV INFO); hot cues and loops unchanged after editing title, artist, genre and comment |
| [REQ-006](requirements.md#req-006) | Edit title, artist, key, BPM, comment and label of an MP3 copy with CrateEdit, then import it into Rekordbox: all six values appear as written. | 13 Oct 2026: passed after [ADR-0012](adr/0012-plain-comment-frame.md). Title, artist, album, genre, year, label, key and comment shown (comment re-tested in Rekordbox and Serato). BPM is never read from tags by Rekordbox (also 0.00 for an unedited file with BPM 115), so it is covered by [REQ-131](requirements.md#req-131) |
| [REQ-011](requirements.md#req-011) | Edit a copy of a track on the Google Drive for desktop mount and let it sync. In Drive on the web, the file is still one file (with version history), and no conflict copies appear in the folder. | 13 Oct 2026: passed. One file with two versions in Drive, no conflict copies |
| [REQ-092](requirements.md#req-092) | Run the real-file tests with `CRATEEDIT_WRITE_DIR` pointing to a folder on the Google Drive mount and on a USB stick: all safe-write and crash-recovery tests pass, and the folder is empty afterwards. | 13 Oct 2026, feat/safe-file-writer: Google Drive for desktop and a FAT32 USB stick, 6 samples each, all passed |
| [REQ-003](requirements.md#req-003) | Edit title, artist, album, genre, comment and year of a WAV copy with CrateEdit, including umlauts and accents, then import it into Rekordbox: all six values appear as written. | 13 Oct 2026: passed. Bandcamp WAV with umlauts: title, artist, album, genre, comment, year shown correctly; label, BPM, key not shown, as expected |
