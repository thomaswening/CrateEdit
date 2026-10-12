# CrateEdit: Implementation Roadmap
Draft | 9 October 2026 | Based on requirements v0.4

## Principles
- **Risky foundations first.** Tag preservation and safe writes are the parts that can damage a user's collection, and every later phase builds on them, so they are proven before any UI depends on them.
- **Every phase ends with a runnable app** that does something useful, even if read-only.
- **Open decisions are resolved just before the phase that needs them**, not all up front. The "Needs" line of each phase lists which ones.
- **Guardrails are verified automatically on every change.** Each phase adds unit, integration and end-to-end tests for its requirements, tagged with requirement IDs; CI must pass before merging (see [development.md](development.md)).
- **Shortcuts and commands go through one registry from day one.** Many requirements say "quick action and configurable shortcut", so actions are defined once and bound to keys later, instead of retrofitting.

## Milestone 1 — Read-only collection browser

### Phase 0: Foundations
Solution skeleton: `CrateEdit.App` (Avalonia + Material.Avalonia, CommunityToolkit.Mvvm), `CrateEdit.Core` (domain, no UI dependency), `CrateEdit.Tests`. CI on GitHub Actions building and testing on Windows and macOS. App-data location, local diagnostic logging with 30-day cleanup, command/shortcut registry with default bindings, English/German UI resources and text-matching rules, release-please and the requirements traceability report.
- Requirements: [REQ-108](requirements.md#req-108) (groundwork), [REQ-109](requirements.md#req-109), [REQ-116](requirements.md#req-116), [REQ-117](requirements.md#req-117), [REQ-118](requirements.md#req-118), [REQ-123](requirements.md#req-123) to [REQ-129](requirements.md#req-129)
- Needs: nothing

### Phase 1: Tag layer and write spike
Read and write the 9 logical fields on MP3 and WAV with CrateEdit's own frame-level tag editor (TagLib# failed the preservation spike, see [ADR-0008](adr/0008-own-id3v2-frame-editor.md)), mapped to one consistent field model. Automated tests on real Rekordbox/Serato/Traktor-tagged files proving a write changes only the edited fields and preserves audio and all other frames (GEOB, cues, beatgrids). Spike the safe-write technique (temporary file + atomic replace, verification) on local disk, USB and a Google Drive mount.
- Requirements: [REQ-003](requirements.md#req-003), [REQ-006](requirements.md#req-006), [REQ-007](requirements.md#req-007), [REQ-088](requirements.md#req-088), [REQ-089](requirements.md#req-089), [REQ-091](requirements.md#req-091), [REQ-130](requirements.md#req-130)
- Resolves: [OPEN-C](requirements.md#open-c), [OPEN-D](requirements.md#open-d)
- Needs: sample files from the user's collection (worked on as copies only)

### Phase 2: Index, initial scan, collection table
SQLite index in app data. Initial scan of the collection root with progress and a responsive UI. Virtualized, sortable, searchable table with configurable and persisted columns. Fast startup from the index with a loading indication. NAS warning when choosing the root. Interim playback: a quick action that opens the track in the operating system's default media player (replaced by the built-in player in Phase 10).
- Requirements: [REQ-002](requirements.md#req-002), [REQ-005](requirements.md#req-005), [REQ-010](requirements.md#req-010), [REQ-012](requirements.md#req-012), [REQ-015](requirements.md#req-015), [REQ-016](requirements.md#req-016), [REQ-026](requirements.md#req-026), [REQ-027](requirements.md#req-027), [REQ-034](requirements.md#req-034), [REQ-035](requirements.md#req-035), [REQ-045](requirements.md#req-045)
- Needs: [OPEN-N](requirements.md#open-n) (date added for the initial scan)

### Phase 3: Collection synchronization
Detect added, removed, renamed, moved and externally modified files at startup and at runtime. Move/rename recognition, case-only renames, distinct copies. Unavailable root and vanished subfolders. Session activity history with severity levels.
- Requirements: [REQ-011](requirements.md#req-011), [REQ-017](requirements.md#req-017) to [REQ-023](requirements.md#req-023), [REQ-121](requirements.md#req-121), [REQ-122](requirements.md#req-122), [REQ-019](requirements.md#req-019)
- Needs: [OPEN-F](requirements.md#open-f) (move/rename identity for all tracks)

## Milestone 2 — Edit and apply safely

### Phase 4: Pending workflow and views
Pending as an app-managed property, new tracks marked pending after the initial scan, pending view (side by side with the full collection), pending column, mark/unmark quick action, live counts, folder restriction.
- Requirements: [REQ-028](requirements.md#req-028), [REQ-029](requirements.md#req-029), [REQ-036](requirements.md#req-036), [REQ-039](requirements.md#req-039) to [REQ-044](requirements.md#req-044), [REQ-042](requirements.md#req-042) (undo, via the Phase 5 undo stack if done together)
- Needs: [OPEN-F](requirements.md#open-f) (already resolved in Phase 3)

### Phase 5: Editing and staging
Durable staged-edit model, inline editing, batch editor with leave/set/clear semantics, keyboard traversal, suggestions, staging view with review, discard and staging timestamps, staging indicators everywhere, session undo/redo with grouped actions.
- Requirements: [REQ-037](requirements.md#req-037), [REQ-038](requirements.md#req-038), [REQ-046](requirements.md#req-046) to [REQ-059](requirements.md#req-059), [REQ-061](requirements.md#req-061), [REQ-062](requirements.md#req-062), [REQ-113](requirements.md#req-113) to [REQ-115](requirements.md#req-115)
- Needs: [OPEN-L](requirements.md#open-l) (staged vs. on-disk values in table, search, filters, suggestions)

### Phase 6: Apply (happy path and failure handling)
Apply selected staged changes using the Phase 1 write technique, with local recovery material, progress, independent per-track failures, bounded retry with shared-cause detection, result summary, pending-after-apply choice, and reconciliation after an interrupted apply.

Release packaging: the release workflow builds unsigned Windows and macOS packages and attaches them to the GitHub release.

Interim safety for the first release (replaced by Phases 7 and 8):
- **Stale-value check instead of conflict resolution.** Immediately before writing a track, its on-disk values are compared with the values captured when the edit was staged. If any field changed externally (including artist/title), the track is skipped, left staged and reported. This keeps [REQ-074](requirements.md#req-074) (no silent overwrite) true from the first release, without the preflight and resolution UI.
- **Simple cancel.** Cancelling finishes in-flight tracks and stops, keeping what was applied ([REQ-082](requirements.md#req-082) behavior only, no revert prompt).
- Requirements: [REQ-013](requirements.md#req-013), [REQ-055](requirements.md#req-055), [REQ-060](requirements.md#req-060), [REQ-063](requirements.md#req-063), [REQ-064](requirements.md#req-064), [REQ-075](requirements.md#req-075), [REQ-077](requirements.md#req-077) to [REQ-080](requirements.md#req-080), [REQ-088](requirements.md#req-088) to [REQ-098](requirements.md#req-098)
- Needs: [OPEN-C](requirements.md#open-c) (resolved in Phase 1), [OPEN-I](requirements.md#open-i) (closing during a running apply)

### First release (v0.1.0)
Published on GitHub Releases at the end of Milestone 2, without code signing and with an explicit "use at your own risk" notice. Release notes list the interim behaviors above and the not-yet-implemented requirements (filters, built-in player, shortcut configuration UI, conflict resolution, revert on cancel). Later milestones ship as further minor releases.

## Milestone 3 — Robustness

### Phase 7: External changes and conflicts
Detect external tag changes against staged edits, conflict markers, per-field resolution, identity-change decisions, consolidated preflight with bulk decisions, concurrent-modification protection and the unavailable-protection prompt.
- Requirements: [REQ-065](requirements.md#req-065) to [REQ-074](requirements.md#req-074), [REQ-076](requirements.md#req-076)
- Needs: [OPEN-H](requirements.md#open-h) (protection duration)

### Phase 8: Cancellation, keep and revert
Cancel pauses after in-flight tracks, keep/revert/continue prompt, revert restoring file and original staged edit, retained recovery material, blocked close during the prompt.
- Requirements: [REQ-081](requirements.md#req-081) to [REQ-087](requirements.md#req-087)
- Needs: [OPEN-G](requirements.md#open-g) (revert failure), [OPEN-J](requirements.md#open-j) (crash during the prompt)

## Milestone 4 — Complete first release

### Phase 9: Filters
Saved, named filters, empty-field filters, no filter applied at startup, stable working set with the "no longer matches" marker.
- Requirements: [REQ-030](requirements.md#req-030) to [REQ-033](requirements.md#req-033)
- Needs: [OPEN-L](requirements.md#open-l) (resolved in Phase 5)

### Phase 10: Playback
One global player with drag-and-drop/quick-action/shortcut loading, controls, volume, looping, loaded-track indication, uninterrupted playback while editing.
- Requirements: [REQ-099](requirements.md#req-099) to [REQ-107](requirements.md#req-107)
- Needs: [OPEN-K](requirements.md#open-k) (applying to the playing track)
- Note: independent of Phases 4–9 apart from [OPEN-K](requirements.md#open-k); can be moved earlier if you want to listen while tagging sooner.

### Phase 11: Settings, shortcuts and shutdown
Shortcut configuration UI with conflict prevention, persisted preferences, quiet shutdown, optional shutdown reminder.
- Requirements: [REQ-108](requirements.md#req-108) to [REQ-112](requirements.md#req-112)
- Needs: [OPEN-M](requirements.md#open-m) (reserved shortcuts and contexts)

### Phase 12: Hardening and release
Performance benchmarks against [REQ-015](requirements.md#req-015)/[REQ-106](requirements.md#req-106) on the minimum configuration, macOS pass, cloud-drive and USB disconnect testing, diagnostic export.
- Requirements: [REQ-015](requirements.md#req-015), [REQ-098](requirements.md#req-098), [REQ-106](requirements.md#req-106) (verification)

## Open decisions by phase
| Decision | Needed before |
|---|---|
| [OPEN-C](requirements.md#open-c), [OPEN-D](requirements.md#open-d) | resolved by Phase 1 spike |
| [OPEN-N](requirements.md#open-n) | Phase 2 |
| [OPEN-F](requirements.md#open-f) | Phase 3 |
| [OPEN-L](requirements.md#open-l) | Phase 5 |
| [OPEN-I](requirements.md#open-i) | Phase 6 |
| License | before the first public release (ideally Phase 0) |
| [OPEN-H](requirements.md#open-h) | Phase 7 |
| [OPEN-G](requirements.md#open-g), [OPEN-J](requirements.md#open-j) | Phase 8 |
| [OPEN-K](requirements.md#open-k) | Phase 10 |
| [OPEN-M](requirements.md#open-m) | Phase 11 |
