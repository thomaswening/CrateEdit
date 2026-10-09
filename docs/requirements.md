# CrateEdit: Verifiable Product Requirements
Version: 0.4 draft | 9 October 2026
Status: Consistency pass on v0.3. Open decisions are collected in section 12 (OPEN-C, OPEN-D, OPEN-F to OPEN-N). Supersedes v0.3.

## Requirement conventions
**SHALL** = mandatory for the stated release; **SHOULD** = preferred, with an acceptable documented exception; **COULD** = optional or future capability. Each requirement has a stable ID. Conditions are expressed as **WHEN … THEN …**, **IF … THEN …**, **BEFORE …**, **WHILE …**, or **AFTER …**. An implementation technique is not a product requirement unless explicitly stated. Items marked **OPEN** require a decision before they can be treated as acceptance criteria. **OUT OF SCOPE** marks a capability explicitly not required for the first release.

**Quick action** (used throughout): a control that performs an action in a single interaction, without opening a dialog, wizard, or intermediate confirmation step — e.g. a button in the row or toolbar, a right-click context-menu entry, or a keyboard shortcut. Distinguished from actions that require navigating a settings screen or multi-step editor first. The specific UI mechanism is left to design.

## 1. Scope and collection

**REQ-001 — Purpose (SHALL).** The application SHALL enable users to inspect and edit metadata for a DJ music collection without requiring playlist management or general file-management workflows.

**REQ-002 — Collection root (SHALL).** WHEN a user configures a collection root, the application SHALL include supported audio files in that directory and its subdirectories.

**REQ-003 — Formats (SHALL).** The application SHALL support reading and editing metadata in MP3 and WAV files.

**REQ-004 — Additional formats (COULD).** The application COULD support AIFF, FLAC, M4A and AAC, provided the same safety and field-consistency requirements are met.

**REQ-005 — Collection scale (SHALL).** The application SHALL support a collection containing up to 20,000 tracks.

**REQ-006 — Consistent fields (SHALL).** WHEN displaying files of different supported formats, the application SHALL expose a consistent set of logical metadata fields rather than format-specific tag identifiers.

**REQ-007 — Initial fields (SHALL).** The first release SHALL support artist, title, album, genre, label, BPM, key, comment and year as logical editable fields. A field may contain multiple comma-separated values (e.g. multiple artists) as free text; the application SHALL NOT attempt to parse or split it into separate values.

**REQ-008 — Rating (COULD).** A future release COULD support a star rating field, contingent on verifying a consistent mapping to the rating representations used by Rekordbox, Serato and Traktor. Deferred from v1 because the interop behavior is not currently known with confidence.

**REQ-009 — Custom fields (COULD).** A future release COULD support user-defined logical metadata fields consistently across formats.

## 2. System requirements and storage locations

**REQ-010 — Minimum supported configuration (SHALL).** The minimum supported configuration SHALL be SSD-based local storage and 8 GB RAM on a currently vendor-supported version of Windows or macOS. This configuration SHALL serve as the benchmark environment for REQ-015 and REQ-106.

**REQ-011 — Supported storage locations (SHALL).** The application SHALL support a collection root located on internal disk, external/USB disk, or a cloud-sync desktop mount (e.g. Google Drive, OneDrive, Dropbox).

**REQ-012 — Unsupported network storage (SHALL).** WHEN a user selects a collection root that appears to be a network location (e.g. a NAS), THEN the application SHALL warn that it is unsupported before proceeding. Detection is best effort and SHALL NOT flag the supported cloud-sync mounts of REQ-011 (e.g. the Google Drive for desktop virtual drive).

**REQ-013 — Local recovery storage (SHALL).** Recovery/backup material used to protect in-flight writes SHALL be stored on local application storage, independent of the collection root, so that it survives the collection root becoming unavailable.

**REQ-014 — Single-machine access (OUT OF SCOPE).** The application need not support the same collection being accessed from more than one machine at a time; concurrent multi-machine access is explicitly out of scope for the first release.

## 3. Performance and collection synchronization

**REQ-015 — Startup target (SHOULD).** WHEN a previously indexed collection is opened, the application SHOULD display the collection table, populated from the index, within five seconds. WHILE the collection is still loading, the application SHALL show a loading indication and SHALL NOT prevent the user from using any part of the application that does not require all tracks to be loaded. Measurement: from process launch to the table being displayed with its rows loaded from the index, on the minimum configuration defined in REQ-010, using a 20,000-track collection on SSD with the root reachable; the result is the median of 5 cold launches. Background work (e.g. change detection) MAY continue after that point.

**REQ-016 — Initial scan (SHALL).** IF the collection requires initial indexing, THEN the application SHALL display progress and remain responsive.

**REQ-017 — Automatic synchronization (SHALL).** WHEN supported files are added, removed, renamed, moved or externally modified, the application SHALL detect and reflect the change at startup and during runtime without requiring a manual reindex.

**REQ-018 — Ordinary changes (SHALL).** WHEN an addition or removal does not affect staged changes or recovery, the application SHALL update the index without an interrupting dialog.

**REQ-019 — Activity history (SHALL).** WHEN the application detects a notable collection or synchronization event during a session, it SHALL record it in an inspectable, nonblocking session activity history, each entry tagged with a severity level (**info**, **warning**, **error**). This history need not survive restart. Notable events include: file added (info); file removed (info); file renamed or moved (info); ambiguous move/rename with no auto-match attempted (warning); a track with staged changes going missing (warning); a previously-missing track reappearing (warning); an external change conflicting with a staged edit (error).

**REQ-020 — Targeted move matching (SHOULD).** WHEN a file with staged changes disappears and a potential replacement appears within the collection, the application SHOULD attempt to preserve the file instance's identity and staged edits, using expensive checks only for relevant candidates. **OPEN-F:** extend to all tracks.

**REQ-021 — Ambiguous identity (SHALL).** IF a moved or renamed file cannot be identified unambiguously, THEN the application SHALL NOT automatically attach its staged changes to another file.

**REQ-022 — Case-only renames (SHALL).** WHEN a file is renamed only by letter case on a case-insensitive filesystem, the application SHALL treat it as the same track, not as a new or pending track.

**REQ-023 — Distinct copies (SHALL).** WHEN a file is duplicated, the application SHALL treat the two file instances as distinct tracks, even if their audio content matches.

**REQ-024 — Missing staged files (SHALL).** IF a file with staged changes cannot be found, THEN the application SHALL preserve those changes, identify the affected track, offer understandable recovery guidance, and permit a later apply attempt or explicit staging discard.

**REQ-025 — No implicit file deletion (SHALL).** WHEN the user discards staged changes for a missing or damaged track, the application SHALL NOT delete the underlying audio file.

**REQ-121 — Unavailable collection root (SHALL).** IF the collection root cannot be reached (e.g. an unplugged USB drive or a disconnected cloud-sync mount), THEN the application SHALL treat the entire collection as unavailable rather than as removed. It SHALL retain the index, staged changes and pending flags; mark all tracks unavailable; disable apply and playback while still permitting editing and staging; NOT count unavailable tracks as missing or pending; and show a persistent, nonblocking notice with a retry action. WHEN the root becomes reachable again, the application SHALL resynchronize automatically (REQ-017) and report only genuine differences through the activity history.

**REQ-122 — Vanished subfolder (SHALL).** IF the collection root is reachable but a subfolder disappears, THEN the application SHALL treat it as an ordinary removal (REQ-017), making its tracks missing (with REQ-024 applying to tracks with staged changes). EXCEPTION (best effort): IF the vanished subfolder was a link or mount point whose target is unreachable, THEN the application SHOULD treat its tracks as unavailable as in REQ-121.

## 4. Views, filters and pending status

**REQ-026 — Collection table (SHALL).** The application SHALL provide a searchable, sortable table with configurable column visibility and order, including supported metadata and relevant file properties such as filename and timestamps. **OPEN-L:** staged vs. on-disk values.

**REQ-027 — Select all (SHALL).** The application SHALL let the user select all tracks currently visible in a view (i.e. after filtering) with a single action, in addition to standard multi-select (click, shift-click, ctrl/cmd-click).

**REQ-028 — Working views (SHALL).** The application SHALL make the full collection, pending tracks and staged changes directly accessible; full and pending views SHALL be capable of being displayed simultaneously.

**REQ-029 — Folder restriction (SHALL).** WHEN a user selects a subdirectory, the application SHALL allow the displayed collection to be restricted to it.

**REQ-030 — Saved filters (SHALL).** The application SHALL let users create, name, save, apply and manage multiple filters.

**REQ-031 — Empty-field filters (SHALL).** WHEN configuring a filter, the user SHALL be able to select metadata fields and match tracks for which at least one selected field is empty.

**REQ-032 — Startup filter (SHALL).** WHEN the application starts, it SHALL NOT automatically apply a saved filter.

**REQ-033 — Stable editing set (SHALL).** WHEN a staged edit causes a track to cease matching an active filter, the application SHALL keep the track visible in the current working set, marked as no longer matching the filter, until the user explicitly refreshes or reapplies the filter.

**REQ-034 — Persisted view state (SHALL).** WHEN the user changes a view's columns, column order or sort order, the application SHALL restore the relevant view state in later sessions, except for an explicitly defined initial sort.

**REQ-035 — Date added (SHALL).** The application SHALL record, per track, an application-managed "date added to collection" timestamp, independent of the filesystem's creation-time property. **OPEN-N:** initial-scan seeding.

**REQ-036 — Pending sort (SHALL).** WHEN the pending view is initially opened in a session, it SHALL default to sorting by the date-added timestamp defined in REQ-035; the user SHALL be able to change that sort.

**REQ-037 — Staging sort (SHALL).** WHEN the staging view is initially opened, it SHALL default to sorting by each track's most recent staging timestamp, independently of other views.

**REQ-038 — Staging indicator (SHALL).** IF a track has unapplied staged changes, THEN the application SHALL visibly indicate that state wherever the track appears.

**REQ-039 — Pending status (SHALL).** The application SHALL maintain pending as an application-managed track property, not an audio metadata field.

**REQ-040 — New tracks (SHALL).** WHEN an unrecognized file is added to the collection **after** the initial scan, the application SHALL mark it pending. Tracks found during the initial scan of a newly configured collection SHALL NOT be marked pending. **OPEN-F:** definition of "unrecognized".

**REQ-041 — Explicit pending changes (SHALL).** The application SHALL let users mark or unmark one or multiple selected tracks pending through a quick action and configurable shortcut, independently of metadata editing or application.

**REQ-042 — Pending undo (SHALL).** WHEN the user changes pending status, the action SHALL support undo and redo during the current session.

**REQ-043 — Pending column (SHALL).** The pending property SHALL be available as a configurable column and SHALL be hidden by default.

**REQ-044 — Pending counts (SHALL).** The application SHALL display at a glance, from startup onward and kept current during the session, the number of pending tracks and the number of tracks with staged changes, without a blocking dialog. IF any tracks with staged changes are missing, it SHALL also display their count separately; missing tracks SHALL NOT contribute to the pending count.

**REQ-045 — Reveal file location (SHALL).** The application SHALL let the user open the selected track's containing folder in the operating system's file browser through a quick action.

## 5. Editing and staging

**REQ-046 — Inline editing (SHALL).** The application SHALL support editing individual metadata fields directly in the collection table.

**REQ-047 — Batch editing (SHALL).** WHEN multiple tracks are selected, the application SHALL offer a batch editor accessible by a configurable shortcut.

**REQ-048 — Batch field semantics (SHALL).** For each batch-edited field, the application SHALL distinguish leave unchanged, set a value and clear the value; it SHALL NOT overwrite fields left unchanged.

**REQ-049 — Keyboard workflow (SHALL).** The application SHALL support configurable vertical and horizontal edit traversal and a quick accept-and-close action; after closing the editor, it SHALL restore table focus and preserve the selection.

**REQ-050 — Initial editor focus (SHOULD).** WHEN the batch editor opens from a selected table column, it SHOULD initially focus the corresponding field.

**REQ-051 — Suggestions (SHALL).** WHEN a user edits a supported metadata field, the application SHALL offer relevant suggestions from values already in the collection, including values that are staged but not yet applied. A comma-separated field's suggestions SHALL be offered as whole previously-used values, not split into individual components.

**REQ-052 — Universal staging (SHALL).** WHEN an edit is accepted, the application SHALL stage it rather than immediately writing to the audio file; the first release SHALL NOT provide a direct-write editing mode.

**REQ-053 — Durable staging (SHALL).** WHEN a staging operation is shown as complete, its resulting state SHALL already be durably persisted and recoverable after a crash or power loss. Unaccepted text currently being typed need not be recovered.

**REQ-054 — Persistent staged state (SHALL).** WHEN the application is restarted, it SHALL restore unapplied staged changes.

**REQ-055 — Optional review (SHALL).** The application SHALL let the user apply staged changes without first opening a dedicated review view.

**REQ-056 — Review table (SHALL).** WHEN the staging view is opened, it SHALL show only tracks with staged changes, highlight modified fields, and let the user inspect their previous values and correct staged values.

**REQ-057 — Field and track discard (SHALL).** WHEN reviewing staging, the user SHALL be able to discard the staged change for an individual field or discard all staged changes for one or multiple selected tracks using quick actions.

**REQ-058 — Undoable discard (SHALL).** A staging discard SHALL be undoable during the session and SHALL NOT require a confirmation dialog.

**REQ-059 — Staging timestamp (SHALL).** Each staged track SHALL expose the date and time of its most recent staged edit for sorting and filtering.

**REQ-060 — Apply selection (SHALL).** The application SHALL let the user apply staged changes for selected tracks from ordinary views or staging, leaving unselected tracks staged.

## 6. Undo and redo

**REQ-061 — Session history (SHALL).** The application SHALL support undo and redo for staging operations and pending-status changes during the current session; it SHALL NOT be required to restore the undo/redo history after restart.

**REQ-062 — Grouped action (SHALL).** WHEN a single user operation edits multiple tracks, one undo or redo SHALL treat that operation as one logical step.

**REQ-063 — Apply boundary (SHALL).** WHEN changes for a track have been successfully applied to its audio file and are finalized, the application SHALL remove those applied changes from staging and from undo/redo history. Applied changes are finalized when the apply run completes without cancellation, or when the user chooses "keep" after a cancellation (REQ-082). Until then they remain revertible (REQ-083).

**REQ-064 — Partial apply history (SHALL).** IF one grouped action includes applied and unapplied tracks, THEN subsequent undo/redo of that action SHALL affect only its unapplied members.

## 7. External changes and conflict resolution

**REQ-065 — Nonconflicting edits (SHALL).** WHEN an external application changes a field that does not conflict with staged edits, the application SHALL reflect and preserve that external change.

**REQ-066 — Conflict marker (SHALL).** WHEN an external application changes a field that also has a conflicting staged value, the application SHALL visibly flag the conflict without immediately interrupting the user.

**REQ-067 — Early resolution (SHALL).** WHEN the user opens a conflicted track in staging, the application SHALL allow resolution of each ordinary field conflict by choosing to keep the staged value or the current on-disk value.

**REQ-068 — Identity changes (SHALL).** IF artist or title has changed externally since the user staged edits, THEN the application SHALL require an explicit decision before applying the affected track, even if the staged field itself does not conflict.

**REQ-069 — Consolidated preflight (SHALL).** WHEN an apply operation encounters multiple identity changes or conflicts, the application SHALL present a consolidated overview and allow per-field resolution and appropriate bulk decisions rather than serial per-track prompts.

**REQ-070 — Explicit choices, identity conflicts (SHALL).** For an externally changed artist/title, the user SHALL be able to exclude the track and continue, proceed using the current identity values, or abort and review. The application SHALL NOT silently select a choice. IF the user's staged edits also change artist or title, THEN that field is additionally a field conflict resolved under REQ-071.

**REQ-071 — Explicit choices, field conflicts (SHALL).** For an ordinary field conflict, the user SHALL be able to choose to keep the staged value or the current on-disk value. The application SHALL NOT silently select a choice.

**REQ-072 — Concurrent modification protection (SHALL).** BEFORE validating an apply set, the application SHALL attempt to protect the selected files against concurrent modification and SHALL maintain effective protection through validation, user decisions, writing and any cancellation pause (REQ-081) wherever the underlying environment permits. **OPEN-H:** protection duration during preflight.

**REQ-073 — Unavailable protection (SHALL).** IF protection cannot be established, THEN the application SHALL explain the risk in understandable terms and require the user to choose whether to exclude affected tracks, proceed despite the risk, or abort.

**REQ-074 — No silent overwrite (SHALL).** The application SHALL NOT knowingly overwrite a conflicting external field without explicit resolution.

## 8. Apply execution, cancellation and recovery

**REQ-075 — Explicit apply (SHALL).** The application SHALL write staged metadata to audio files only after the user initiates apply and any required preflight decisions have been resolved.

**REQ-076 — Unattended execution (SHOULD).** WHEN preflight decisions are complete, the apply operation SHOULD proceed without further prompts. IF a new conflict is detected mid-apply for a track where protection was unavailable, THEN it SHALL be treated as an independent per-track failure (REQ-078) rather than pausing the batch to prompt the user.

**REQ-077 — Progress (SHALL).** WHILE apply is running, the application SHALL communicate progress and nonblocking failure information and SHALL remain usable for unrelated tracks.

**REQ-078 — Independent failures (SHALL).** IF applying one track fails, THEN the application SHALL continue with independent tracks unless the user cancels or a shared failure prevents useful progress.

**REQ-079 — Automatic retry (SHALL).** IF a failure is recoverable, THEN the application SHALL attempt a bounded automatic retry. Recoverable failures include a file transiently locked by another process and a transient storage timeout or disconnect (including a cloud-sync mount momentarily unreachable). Non-recoverable failures — not retried — include a missing file (REQ-024), permission denial, disk full, a corrupt or unparseable file, a path exceeding filesystem limits, and the drive becoming entirely unavailable. IF the same underlying problem affects multiple tracks, THEN the application SHALL detect the shared cause after a bounded number of failures (exact bound set during design) and avoid redundant retries for each remaining track. IF a disconnect persists beyond the bounded retry window, THEN the collection root SHALL be treated as unavailable (REQ-121).

**REQ-080 — Failed staging (SHALL).** IF a track is not successfully applied, THEN its unapplied changes SHALL remain staged for later retry or explicit discard.

**REQ-081 — Cancellation pause (SHALL).** WHEN the user cancels an apply operation, the application SHALL finish the tracks currently being written, then pause before starting any further track, and present three choices: keep the changes applied so far and stop; revert the changes applied so far and stop; or continue the apply operation.

**REQ-082 — Keep on cancel (SHALL).** IF the user chooses to keep, THEN the tracks applied during that run SHALL be finalized exactly as an uninterrupted apply would (REQ-063 applies).

**REQ-083 — Revert on cancel (SHALL).** IF the user chooses to revert, THEN for each track applied during that run, the application SHALL restore its pre-apply on-disk state using retained recovery material, and SHALL restore its original staged edit exactly as it was before the apply, without requiring the user to redefine it.

**REQ-084 — Revert failure (SHALL).** IF a revert write fails, THEN the affected track SHALL remain in its applied (not reverted) state and the failure SHALL be reported like any other apply failure. **OPEN-G:** what stays staged afterwards (the original edit, or the revert as a staged edit).

**REQ-085 — Retained recovery material for cancellation (SHALL).** The application SHALL retain a cancelled apply run's pre-apply recovery material until the keep/revert decision for that run is resolved, as an explicit exception to REQ-090.

**REQ-086 — No close during cancellation prompt (SHALL).** WHILE the keep/revert/continue prompt from REQ-081 is open, the application SHALL prevent user-initiated closing. (A crash or OS shutdown cannot be prevented; see OPEN-J.)

**REQ-087 — No rollback outside cancellation (OUT OF SCOPE).** A completed apply operation that was not cancelled SHALL NOT be subject to a separate retroactive rollback; reverting an already-fully-applied, uncancelled operation is out of scope for the first release.

**REQ-088 — Safe writes (SHALL).** The application SHALL preserve audio and unrelated metadata — including proprietary tag frames used by Rekordbox, Serato and Traktor (e.g. cue points, beatgrids, hot cues, GEOB blocks) — and SHALL NOT knowingly leave a partially written or corrupted audio file as a successful result.

**REQ-089 — Recoverability (SHALL).** BEFORE exposing an audio file to write risk, the application SHALL preserve sufficient recovery material to restore its pre-write state; it SHALL verify successful completion before discarding that material (subject to the REQ-085 exception during an unresolved cancellation).

**REQ-090 — No permanent write backup (SHALL).** The application SHALL NOT require retaining a full-file backup or separate pre-write metadata snapshot after the corresponding write has been safely completed, verified, and any cancellation decision resolved.

**REQ-091 — Bounded temporary storage (SHALL).** The application SHALL bound temporary storage used by in-flight writes; the exact write technique, backup representation and concurrency level SHALL be determined during design and benchmarking.

**REQ-092 — Interrupted apply (SHALL).** WHEN the application restarts after an interrupted apply, it SHALL reconcile actual file state, staged changes and recovery material; it SHALL NOT blindly reapply already successful edits or discard unsuccessful edits.

**REQ-093 — Restoration (SHALL).** IF restoration is needed, THEN the application SHALL attempt safe automatic recovery. IF it cannot restore a track, THEN it SHALL retain the unresolved recovery state, prominently inform the user, provide available remedies, and prevent both editing and playback of the affected track while leaving other tracks usable.

**REQ-094 — No forced resume (SHALL).** WHEN the application restarts after interrupted apply, it SHALL NOT automatically resume the unfinished batch or require a blocking resume dialog; the user SHALL be able to access remaining staging directly.

**REQ-095 — Pending after apply (SHALL).** IF successfully applied tracks are pending, THEN the application SHALL offer one all-or-nothing choice to clear pending status for those tracks; clearing pending SHALL NOT be automatic.

**REQ-096 — Result summary (SHALL).** WHEN an apply attempt finishes, the application SHALL show a nonblocking, revisit-able summary of successes and failures, grouped by reason, with affected files and actionable remedies.

**REQ-097 — Summary replacement (SHALL).** WHEN another apply attempt completes, its summary SHALL replace the previous apply summary rather than accumulating a history in that view.

**REQ-098 — Diagnostic detail (SHALL).** The application SHALL make sufficient error detail available for copying or export and later investigation, subject to the retention and privacy requirements in section 11.

## 9. Playback

**REQ-099 — Single player (SHALL).** The application SHALL provide one global player for one track at a time, without a queue or automatic next-track playback.

**REQ-100 — Explicit playback (SHALL).** WHEN a user explicitly loads a track into the player, playback SHALL begin immediately; merely selecting a row SHALL NOT start playback.

**REQ-101 — Loading methods (SHALL).** The user SHALL be able to load a track through drag and drop, a quick track action, and a configurable shortcut.

**REQ-102 — Loaded-track indication (SHALL).** The application SHALL identify the loaded track in relevant views and display at least its artist and title in the player.

**REQ-103 — Controls (SHALL).** The player SHALL provide play, pause, restart, seek, duration, elapsed time, and an immediately accessible volume control.

**REQ-104 — Looping (SHALL).** The player SHALL provide a one-click loop toggle. WHEN playback reaches the end, it SHALL restart if looping is enabled; otherwise it SHALL stop and return to the beginning.

**REQ-105 — Editing during playback (SHALL).** WHEN metadata changes, playback SHALL continue without an interruption caused solely by the tag change. **OPEN-K:** applying to the playing track.

**REQ-106 — Playback responsiveness (SHOULD).** Explicit playback SHOULD start within one second. Measurement: from the load action (drag and drop, quick action or shortcut) to first audible output, for a local MP3 and a local WAV on the minimum configuration defined in REQ-010; files on a cloud-sync mount are excluded.

**REQ-107 — Additional player features (COULD).** The player COULD show remaining time, a waveform or support system media keys.

## 10. Settings, startup and shutdown

**REQ-108 — Configurable shortcuts (SHALL).** The user SHALL be able to configure and persist action shortcuts.

**REQ-109 — Shortcut uniqueness (SHALL).** IF a user assigns a shortcut already assigned to another configurable action, THEN the application SHALL prevent the conflicting assignment; a configurable shortcut SHALL map unambiguously to one action. **OPEN-M:** reserved keys and contexts.

**REQ-110 — Persisted preferences (SHALL).** The application SHALL persist saved filters, per-view layout preferences and configured shortcuts across sessions.

**REQ-111 — Quiet shutdown (SHALL).** WHEN the user closes the application with staged changes, it SHALL preserve them and allow closing without requiring an action, except as restricted by REQ-086. **OPEN-I:** closing during a running apply.

**REQ-112 — Optional shutdown reminder (COULD).** The application COULD offer a dismissible reminder explaining that staged changes will be available next session, with a 'do not remind me again' option.

**REQ-113 — Restored staging notice (SHALL).** WHEN startup restores staged changes, the application SHALL display a visible, nonblocking notification that dismisses automatically without requiring a click.

**REQ-114 — Persistent attention cue (SHALL).** AFTER staged changes are restored, the staging entry point SHALL show a subtle indicator until the user opens the staging view for the first time in that session.

**REQ-115 — Staging access (SHALL).** The user SHALL be able to open staging from the main window in one action.

## 11. Diagnostics and privacy

**REQ-116 — Diagnostic log retention (SHALL).** The application SHALL retain diagnostic/error logs locally for 30 days and SHALL delete them after that period.

**REQ-117 — No automatic transmission (SHALL).** The application SHALL NOT transmit diagnostic logs or error details to any external service automatically; any sharing or export of diagnostic data SHALL be a deliberate, user-initiated action.

**REQ-118 — Minimal personal data in logs (SHOULD).** Diagnostic logs SHOULD avoid including more personal information (e.g. full user-folder paths) than is needed to identify the affected track and error.

**REQ-119 — Crash reporting (COULD).** A future release COULD offer optional crash/diagnostic reporting to the developer. IF offered, THEN it SHALL be opt-in, require explicit consent before any data is transmitted, let the user review what would be sent, and remain disableable at any time.

## 12. Deferred capabilities and unresolved decisions

**REQ-120 — Future extensions (COULD).** Later versions COULD support multiple collection roots, manually curated named track lists, custom fields, external metadata providers and waveform display. These are not first-release acceptance criteria.

**OPEN-C — Safe-write guarantees.** Define supported filesystem assumptions and verification guarantees for safe MP3/WAV writes (REQ-088 to REQ-091). Note: an atomic replace typically requires a temporary file on the same volume as the target, which is distinct from the recovery material kept locally under REQ-013. To be settled by spike.

**OPEN-D — WAV tagging.** WAV metadata (BPM and key in particular) has no single standard tagging convention across Rekordbox, Serato and Traktor (RIFF INFO chunk vs. embedded ID3 chunk support varies). To be settled by testing against real DJ software.

**OPEN-F — Move/rename identity for all tracks.** REQ-020 only attempts move matching for tracks with staged changes, while REQ-040 marks any unrecognized new file pending. Reorganizing a folder of finished tracks would therefore make them all pending and lose their date added. Proposal: recognize moves/renames for all tracks (same size and identical audio content appearing during the same sync), preserving identity, pending status and date added; REQ-021 stays strict for staged tracks. Also defines "unrecognized" in REQ-040.

**OPEN-G — Revert failure (REQ-084).** If a revert write fails, the file already holds the applied value, so keeping the original staged edit makes it a no-op, and the recovery material is discarded once the prompt is resolved, so the revert cannot be retried from it. Proposal: stage the revert itself (the pre-apply values) as an edit, so it can be applied later.

**OPEN-H — Protection duration (REQ-072).** Holding protection from validation through user decisions may block DJ software from reading those files for as long as the user deliberates. Decide whether protection starts at preflight or only when writing begins (with a re-check of on-disk state at that point).

**OPEN-I — Closing during a running apply.** REQ-111 (quiet shutdown) and REQ-086 (blocked close during the cancellation prompt) do not cover a user-initiated close while an apply is running and not paused.

**OPEN-J — Crash during the cancellation prompt.** Define what happens on restart when the application crashed or the OS shut down while the keep/revert/continue prompt was open (e.g. default to keep, or re-offer the choice while recovery material still exists).

**OPEN-K — Applying to the playing track (REQ-105).** Writing tags to the file currently loaded in the player may interrupt or break playback, depending on the write technique. Decide whether a brief interruption during apply is acceptable, or whether the playing track is deferred to the end of the run.

**OPEN-L — Effective values.** Specify whether the collection table, search (REQ-026), filters (REQ-031) and suggestions show and match the staged value (if any) or the on-disk value. Proposal: staged value where present, marked per REQ-038.

**OPEN-M — Reserved shortcuts (REQ-109).** Define how configurable shortcuts interact with non-configurable keys (undo/redo, text-editing keys, edit traversal) and whether the same key may map to different actions in different contexts (table vs. editor).

**OPEN-N — Date added for the initial scan (REQ-035).** All tracks found in the initial scan would share the same date added, making the date-added sort meaningless for them. Proposal: seed date added from the filesystem creation time during the initial scan only; it is app-managed afterwards.
