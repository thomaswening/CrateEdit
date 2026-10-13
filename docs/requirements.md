# CrateEdit: Verifiable Product Requirements
Version: 0.7 draft | 13 October 2026
Status: Adds [REQ-131](#req-131) (fields not shown by DJ software) and resolves [OPEN-D](#open-d) (WAV tagging). Open decisions are collected in section 13 ([OPEN-C](#open-c), [OPEN-D](#open-d), [OPEN-F](#open-f) to [OPEN-N](#open-n)). Supersedes v0.4.

## Requirement conventions
**SHALL** = mandatory for the stated release; **SHOULD** = preferred, with an acceptable documented exception; **COULD** = optional or future capability. Each requirement has a stable ID. Conditions are expressed as **WHEN … THEN …**, **IF … THEN …**, **BEFORE …**, **WHILE …**, or **AFTER …**. An implementation technique is not a product requirement unless explicitly stated. Items marked **OPEN** require a decision before they can be treated as acceptance criteria. **OUT OF SCOPE** marks a capability explicitly not required for the first release.

**Quick action** (used throughout): a control that performs an action in a single interaction, without opening a dialog, wizard, or intermediate confirmation step — e.g. a button in the row or toolbar, a right-click context-menu entry, or a keyboard shortcut. Distinguished from actions that require navigating a settings screen or multi-step editor first. The specific UI mechanism is left to design.

## 1. Scope and collection

<a id="req-001"></a>**REQ-001 — Purpose (SHALL).** The application SHALL enable users to inspect and edit metadata for a DJ music collection without requiring playlist management or general file-management workflows.

<a id="req-002"></a>**REQ-002 — Collection root (SHALL).** WHEN a user configures a collection root, the application SHALL include supported audio files in that directory and its subdirectories.

<a id="req-003"></a>**REQ-003 — Formats (SHALL).** The application SHALL support reading and editing metadata in MP3 and WAV files.

<a id="req-004"></a>**REQ-004 — Additional formats (COULD).** The application COULD support AIFF, FLAC, M4A and AAC, provided the same safety and field-consistency requirements are met.

<a id="req-005"></a>**REQ-005 — Collection scale (SHALL).** The application SHALL support a collection containing up to 20,000 tracks.

<a id="req-006"></a>**REQ-006 — Consistent fields (SHALL).** WHEN displaying files of different supported formats, the application SHALL expose a consistent set of logical metadata fields rather than format-specific tag identifiers.

<a id="req-007"></a>**REQ-007 — Initial fields (SHALL).** The first release SHALL support artist, title, album, genre, label, BPM, key, comment and year as logical editable fields. A field may contain multiple comma-separated values (e.g. multiple artists) as free text; the application SHALL NOT attempt to parse or split it into separate values.

<a id="req-008"></a>**REQ-008 — Rating (COULD).** A future release COULD support a star rating field, contingent on verifying a consistent mapping to the rating representations used by Rekordbox, Serato and Traktor. Deferred from v1 because the interop behavior is not currently known with confidence.

<a id="req-009"></a>**REQ-009 — Custom fields (COULD).** A future release COULD support user-defined logical metadata fields consistently across formats.

## 2. System requirements and storage locations

<a id="req-010"></a>**REQ-010 — Minimum supported configuration (SHALL).** The minimum supported configuration SHALL be SSD-based local storage and 8 GB RAM on a currently vendor-supported version of Windows or macOS. This configuration SHALL serve as the benchmark environment for [REQ-015](#req-015) and [REQ-106](#req-106).

<a id="req-011"></a>**REQ-011 — Supported storage locations (SHALL).** The application SHALL support a collection root located on internal disk, external/USB disk, or a cloud-sync desktop mount (e.g. Google Drive, OneDrive, Dropbox).

<a id="req-012"></a>**REQ-012 — Unsupported network storage (SHALL).** WHEN a user selects a collection root that appears to be a network location (e.g. a NAS), THEN the application SHALL warn that it is unsupported before proceeding. Detection is best effort and SHALL NOT flag the supported cloud-sync mounts of [REQ-011](#req-011) (e.g. the Google Drive for desktop virtual drive).

<a id="req-013"></a>**REQ-013 — Local recovery storage (SHALL).** Recovery/backup material used to protect in-flight writes SHALL be stored on local application storage, independent of the collection root, so that it survives the collection root becoming unavailable.

<a id="req-014"></a>**REQ-014 — Single-machine access (OUT OF SCOPE).** The application need not support the same collection being accessed from more than one machine at a time; concurrent multi-machine access is explicitly out of scope for the first release.

## 3. Performance and collection synchronization

<a id="req-015"></a>**REQ-015 — Startup target (SHOULD).** WHEN a previously indexed collection is opened, the application SHOULD display the collection table, populated from the index, within five seconds. WHILE the collection is still loading, the application SHALL show a loading indication and SHALL NOT prevent the user from using any part of the application that does not require all tracks to be loaded. Measurement: from process launch to the table being displayed with its rows loaded from the index, on the minimum configuration defined in [REQ-010](#req-010), using a 20,000-track collection on SSD with the root reachable; the result is the median of 5 cold launches. Background work (e.g. change detection) MAY continue after that point.

<a id="req-016"></a>**REQ-016 — Initial scan (SHALL).** IF the collection requires initial indexing, THEN the application SHALL display progress and remain responsive.

<a id="req-017"></a>**REQ-017 — Automatic synchronization (SHALL).** WHEN supported files are added, removed, renamed, moved or externally modified, the application SHALL detect and reflect the change at startup and during runtime without requiring a manual reindex.

<a id="req-018"></a>**REQ-018 — Ordinary changes (SHALL).** WHEN an addition or removal does not affect staged changes or recovery, the application SHALL update the index without an interrupting dialog.

<a id="req-019"></a>**REQ-019 — Activity history (SHALL).** WHEN the application detects a notable collection or synchronization event during a session, it SHALL record it in an inspectable, nonblocking session activity history, each entry tagged with a severity level (**info**, **warning**, **error**). This history need not survive restart. Notable events include: file added (info); file removed (info); file renamed or moved (info); ambiguous move/rename with no auto-match attempted (warning); a track with staged changes going missing (warning); a previously-missing track reappearing (warning); an external change conflicting with a staged edit (error).

<a id="req-020"></a>**REQ-020 — Targeted move matching (SHOULD).** WHEN a file with staged changes disappears and a potential replacement appears within the collection, the application SHOULD attempt to preserve the file instance's identity and staged edits, using expensive checks only for relevant candidates. **[OPEN-F](#open-f):** extend to all tracks.

<a id="req-021"></a>**REQ-021 — Ambiguous identity (SHALL).** IF a moved or renamed file cannot be identified unambiguously, THEN the application SHALL NOT automatically attach its staged changes to another file.

<a id="req-022"></a>**REQ-022 — Case-only renames (SHALL).** WHEN a file is renamed only by letter case on a case-insensitive filesystem, the application SHALL treat it as the same track, not as a new or pending track.

<a id="req-023"></a>**REQ-023 — Distinct copies (SHALL).** WHEN a file is duplicated, the application SHALL treat the two file instances as distinct tracks, even if their audio content matches.

<a id="req-024"></a>**REQ-024 — Missing staged files (SHALL).** IF a file with staged changes cannot be found, THEN the application SHALL preserve those changes, identify the affected track, offer understandable recovery guidance, and permit a later apply attempt or explicit staging discard.

<a id="req-025"></a>**REQ-025 — No implicit file deletion (SHALL).** WHEN the user discards staged changes for a missing or damaged track, the application SHALL NOT delete the underlying audio file.

<a id="req-121"></a>**REQ-121 — Unavailable collection root (SHALL).** IF the collection root cannot be reached (e.g. an unplugged USB drive or a disconnected cloud-sync mount), THEN the application SHALL treat the entire collection as unavailable rather than as removed. It SHALL retain the index, staged changes and pending flags; mark all tracks unavailable; disable apply and playback while still permitting editing and staging; NOT count unavailable tracks as missing or pending; and show a persistent, nonblocking notice with a retry action. WHEN the root becomes reachable again, the application SHALL resynchronize automatically ([REQ-017](#req-017)) and report only genuine differences through the activity history.

<a id="req-122"></a>**REQ-122 — Vanished subfolder (SHALL).** IF the collection root is reachable but a subfolder disappears, THEN the application SHALL treat it as an ordinary removal ([REQ-017](#req-017)), making its tracks missing (with [REQ-024](#req-024) applying to tracks with staged changes). EXCEPTION (best effort): IF the vanished subfolder was a link or mount point whose target is unreachable, THEN the application SHOULD treat its tracks as unavailable as in [REQ-121](#req-121).

## 4. Views, filters and pending status

<a id="req-026"></a>**REQ-026 — Collection table (SHALL).** The application SHALL provide a searchable, sortable table with configurable column visibility and order, including supported metadata and relevant file properties such as filename and timestamps. **[OPEN-L](#open-l):** staged vs. on-disk values.

<a id="req-027"></a>**REQ-027 — Select all (SHALL).** The application SHALL let the user select all tracks currently visible in a view (i.e. after filtering) with a single action, in addition to standard multi-select (click, shift-click, ctrl/cmd-click).

<a id="req-028"></a>**REQ-028 — Working views (SHALL).** The application SHALL make the full collection, pending tracks and staged changes directly accessible; full and pending views SHALL be capable of being displayed simultaneously.

<a id="req-029"></a>**REQ-029 — Folder restriction (SHALL).** WHEN a user selects a subdirectory, the application SHALL allow the displayed collection to be restricted to it.

<a id="req-030"></a>**REQ-030 — Saved filters (SHALL).** The application SHALL let users create, name, save, apply and manage multiple filters.

<a id="req-031"></a>**REQ-031 — Empty-field filters (SHALL).** WHEN configuring a filter, the user SHALL be able to select metadata fields and match tracks for which at least one selected field is empty.

<a id="req-032"></a>**REQ-032 — Startup filter (SHALL).** WHEN the application starts, it SHALL NOT automatically apply a saved filter.

<a id="req-033"></a>**REQ-033 — Stable editing set (SHALL).** WHEN a staged edit causes a track to cease matching an active filter, the application SHALL keep the track visible in the current working set, marked as no longer matching the filter, until the user explicitly refreshes or reapplies the filter.

<a id="req-034"></a>**REQ-034 — Persisted view state (SHALL).** WHEN the user changes a view's columns, column order or sort order, the application SHALL restore the relevant view state in later sessions, except for an explicitly defined initial sort.

<a id="req-035"></a>**REQ-035 — Date added (SHALL).** The application SHALL record, per track, an application-managed "date added to collection" timestamp, independent of the filesystem's creation-time property. **[OPEN-N](#open-n):** initial-scan seeding.

<a id="req-036"></a>**REQ-036 — Pending sort (SHALL).** WHEN the pending view is initially opened in a session, it SHALL default to sorting by the date-added timestamp defined in [REQ-035](#req-035); the user SHALL be able to change that sort.

<a id="req-037"></a>**REQ-037 — Staging sort (SHALL).** WHEN the staging view is initially opened, it SHALL default to sorting by each track's most recent staging timestamp, independently of other views.

<a id="req-038"></a>**REQ-038 — Staging indicator (SHALL).** IF a track has unapplied staged changes, THEN the application SHALL visibly indicate that state wherever the track appears.

<a id="req-039"></a>**REQ-039 — Pending status (SHALL).** The application SHALL maintain pending as an application-managed track property, not an audio metadata field.

<a id="req-040"></a>**REQ-040 — New tracks (SHALL).** WHEN an unrecognized file is added to the collection **after** the initial scan, the application SHALL mark it pending. Tracks found during the initial scan of a newly configured collection SHALL NOT be marked pending. **[OPEN-F](#open-f):** definition of "unrecognized".

<a id="req-041"></a>**REQ-041 — Explicit pending changes (SHALL).** The application SHALL let users mark or unmark one or multiple selected tracks pending through a quick action and configurable shortcut, independently of metadata editing or application.

<a id="req-042"></a>**REQ-042 — Pending undo (SHALL).** WHEN the user changes pending status, the action SHALL support undo and redo during the current session.

<a id="req-043"></a>**REQ-043 — Pending column (SHALL).** The pending property SHALL be available as a configurable column and SHALL be hidden by default.

<a id="req-044"></a>**REQ-044 — Pending counts (SHALL).** The application SHALL display at a glance, from startup onward and kept current during the session, the number of pending tracks and the number of tracks with staged changes, without a blocking dialog. IF any tracks with staged changes are missing, it SHALL also display their count separately; missing tracks SHALL NOT contribute to the pending count.

<a id="req-045"></a>**REQ-045 — Reveal file location (SHALL).** The application SHALL let the user open the selected track's containing folder in the operating system's file browser through a quick action.

## 5. Editing and staging

<a id="req-046"></a>**REQ-046 — Inline editing (SHALL).** The application SHALL support editing individual metadata fields directly in the collection table.

<a id="req-047"></a>**REQ-047 — Batch editing (SHALL).** WHEN multiple tracks are selected, the application SHALL offer a batch editor accessible by a configurable shortcut.

<a id="req-048"></a>**REQ-048 — Batch field semantics (SHALL).** For each batch-edited field, the application SHALL distinguish leave unchanged, set a value and clear the value; it SHALL NOT overwrite fields left unchanged.

<a id="req-049"></a>**REQ-049 — Keyboard workflow (SHALL).** The application SHALL support configurable vertical and horizontal edit traversal and a quick accept-and-close action; after closing the editor, it SHALL restore table focus and preserve the selection.

<a id="req-050"></a>**REQ-050 — Initial editor focus (SHOULD).** WHEN the batch editor opens from a selected table column, it SHOULD initially focus the corresponding field.

<a id="req-051"></a>**REQ-051 — Suggestions (SHALL).** WHEN a user edits a supported metadata field, the application SHALL offer relevant suggestions from values already in the collection, including values that are staged but not yet applied. A comma-separated field's suggestions SHALL be offered as whole previously-used values, not split into individual components.

<a id="req-052"></a>**REQ-052 — Universal staging (SHALL).** WHEN an edit is accepted, the application SHALL stage it rather than immediately writing to the audio file; the first release SHALL NOT provide a direct-write editing mode.

<a id="req-053"></a>**REQ-053 — Durable staging (SHALL).** WHEN a staging operation is shown as complete, its resulting state SHALL already be durably persisted and recoverable after a crash or power loss. Unaccepted text currently being typed need not be recovered.

<a id="req-054"></a>**REQ-054 — Persistent staged state (SHALL).** WHEN the application is restarted, it SHALL restore unapplied staged changes.

<a id="req-055"></a>**REQ-055 — Optional review (SHALL).** The application SHALL let the user apply staged changes without first opening a dedicated review view.

<a id="req-056"></a>**REQ-056 — Review table (SHALL).** WHEN the staging view is opened, it SHALL show only tracks with staged changes, highlight modified fields, and let the user inspect their previous values and correct staged values.

<a id="req-057"></a>**REQ-057 — Field and track discard (SHALL).** WHEN reviewing staging, the user SHALL be able to discard the staged change for an individual field or discard all staged changes for one or multiple selected tracks using quick actions.

<a id="req-058"></a>**REQ-058 — Undoable discard (SHALL).** A staging discard SHALL be undoable during the session and SHALL NOT require a confirmation dialog.

<a id="req-059"></a>**REQ-059 — Staging timestamp (SHALL).** Each staged track SHALL expose the date and time of its most recent staged edit for sorting and filtering.

<a id="req-060"></a>**REQ-060 — Apply selection (SHALL).** The application SHALL let the user apply staged changes for selected tracks from ordinary views or staging, leaving unselected tracks staged.

## 6. Undo and redo

<a id="req-061"></a>**REQ-061 — Session history (SHALL).** The application SHALL support undo and redo for staging operations and pending-status changes during the current session; it SHALL NOT be required to restore the undo/redo history after restart.

<a id="req-062"></a>**REQ-062 — Grouped action (SHALL).** WHEN a single user operation edits multiple tracks, one undo or redo SHALL treat that operation as one logical step.

<a id="req-063"></a>**REQ-063 — Apply boundary (SHALL).** WHEN changes for a track have been successfully applied to its audio file and are finalized, the application SHALL remove those applied changes from staging and from undo/redo history. Applied changes are finalized when the apply run completes without cancellation, or when the user chooses "keep" after a cancellation ([REQ-082](#req-082)). Until then they remain revertible ([REQ-083](#req-083)).

<a id="req-064"></a>**REQ-064 — Partial apply history (SHALL).** IF one grouped action includes applied and unapplied tracks, THEN subsequent undo/redo of that action SHALL affect only its unapplied members.

## 7. External changes and conflict resolution

<a id="req-065"></a>**REQ-065 — Nonconflicting edits (SHALL).** WHEN an external application changes a field that does not conflict with staged edits, the application SHALL reflect and preserve that external change.

<a id="req-066"></a>**REQ-066 — Conflict marker (SHALL).** WHEN an external application changes a field that also has a conflicting staged value, the application SHALL visibly flag the conflict without immediately interrupting the user.

<a id="req-067"></a>**REQ-067 — Early resolution (SHALL).** WHEN the user opens a conflicted track in staging, the application SHALL allow resolution of each ordinary field conflict by choosing to keep the staged value or the current on-disk value.

<a id="req-068"></a>**REQ-068 — Identity changes (SHALL).** IF artist or title has changed externally since the user staged edits, THEN the application SHALL require an explicit decision before applying the affected track, even if the staged field itself does not conflict.

<a id="req-069"></a>**REQ-069 — Consolidated preflight (SHALL).** WHEN an apply operation encounters multiple identity changes or conflicts, the application SHALL present a consolidated overview and allow per-field resolution and appropriate bulk decisions rather than serial per-track prompts.

<a id="req-070"></a>**REQ-070 — Explicit choices, identity conflicts (SHALL).** For an externally changed artist/title, the user SHALL be able to exclude the track and continue, proceed using the current identity values, or abort and review. The application SHALL NOT silently select a choice. IF the user's staged edits also change artist or title, THEN that field is additionally a field conflict resolved under [REQ-071](#req-071).

<a id="req-071"></a>**REQ-071 — Explicit choices, field conflicts (SHALL).** For an ordinary field conflict, the user SHALL be able to choose to keep the staged value or the current on-disk value. The application SHALL NOT silently select a choice.

<a id="req-072"></a>**REQ-072 — Concurrent modification protection (SHALL).** BEFORE validating an apply set, the application SHALL attempt to protect the selected files against concurrent modification and SHALL maintain effective protection through validation, user decisions, writing and any cancellation pause ([REQ-081](#req-081)) wherever the underlying environment permits. **[OPEN-H](#open-h):** protection duration during preflight.

<a id="req-073"></a>**REQ-073 — Unavailable protection (SHALL).** IF protection cannot be established, THEN the application SHALL explain the risk in understandable terms and require the user to choose whether to exclude affected tracks, proceed despite the risk, or abort.

<a id="req-074"></a>**REQ-074 — No silent overwrite (SHALL).** The application SHALL NOT knowingly overwrite a conflicting external field without explicit resolution.

## 8. Apply execution, cancellation and recovery

<a id="req-075"></a>**REQ-075 — Explicit apply (SHALL).** The application SHALL write staged metadata to audio files only after the user initiates apply and any required preflight decisions have been resolved.

<a id="req-076"></a>**REQ-076 — Unattended execution (SHOULD).** WHEN preflight decisions are complete, the apply operation SHOULD proceed without further prompts. IF a new conflict is detected mid-apply for a track where protection was unavailable, THEN it SHALL be treated as an independent per-track failure ([REQ-078](#req-078)) rather than pausing the batch to prompt the user.

<a id="req-077"></a>**REQ-077 — Progress (SHALL).** WHILE apply is running, the application SHALL communicate progress and nonblocking failure information and SHALL remain usable for unrelated tracks.

<a id="req-078"></a>**REQ-078 — Independent failures (SHALL).** IF applying one track fails, THEN the application SHALL continue with independent tracks unless the user cancels or a shared failure prevents useful progress.

<a id="req-079"></a>**REQ-079 — Automatic retry (SHALL).** IF a failure is recoverable, THEN the application SHALL attempt a bounded automatic retry. Recoverable failures include a file transiently locked by another process and a transient storage timeout or disconnect (including a cloud-sync mount momentarily unreachable). Non-recoverable failures — not retried — include a missing file ([REQ-024](#req-024)), permission denial, disk full, a corrupt or unparseable file, a path exceeding filesystem limits, and the drive becoming entirely unavailable. IF the same underlying problem affects multiple tracks, THEN the application SHALL detect the shared cause after a bounded number of failures (exact bound set during design) and avoid redundant retries for each remaining track. IF a disconnect persists beyond the bounded retry window, THEN the collection root SHALL be treated as unavailable ([REQ-121](#req-121)).

<a id="req-080"></a>**REQ-080 — Failed staging (SHALL).** IF a track is not successfully applied, THEN its unapplied changes SHALL remain staged for later retry or explicit discard.

<a id="req-081"></a>**REQ-081 — Cancellation pause (SHALL).** WHEN the user cancels an apply operation, the application SHALL finish the tracks currently being written, then pause before starting any further track, and present three choices: keep the changes applied so far and stop; revert the changes applied so far and stop; or continue the apply operation.

<a id="req-082"></a>**REQ-082 — Keep on cancel (SHALL).** IF the user chooses to keep, THEN the tracks applied during that run SHALL be finalized exactly as an uninterrupted apply would ([REQ-063](#req-063) applies).

<a id="req-083"></a>**REQ-083 — Revert on cancel (SHALL).** IF the user chooses to revert, THEN for each track applied during that run, the application SHALL restore its pre-apply on-disk state using retained recovery material, and SHALL restore its original staged edit exactly as it was before the apply, without requiring the user to redefine it.

<a id="req-084"></a>**REQ-084 — Revert failure (SHALL).** IF a revert write fails, THEN the affected track SHALL remain in its applied (not reverted) state and the failure SHALL be reported like any other apply failure. **[OPEN-G](#open-g):** what stays staged afterwards (the original edit, or the revert as a staged edit).

<a id="req-085"></a>**REQ-085 — Retained recovery material for cancellation (SHALL).** The application SHALL retain a cancelled apply run's pre-apply recovery material until the keep/revert decision for that run is resolved, as an explicit exception to [REQ-090](#req-090).

<a id="req-086"></a>**REQ-086 — No close during cancellation prompt (SHALL).** WHILE the keep/revert/continue prompt from [REQ-081](#req-081) is open, the application SHALL prevent user-initiated closing. (A crash or OS shutdown cannot be prevented; see [OPEN-J](#open-j).)

<a id="req-087"></a>**REQ-087 — No rollback outside cancellation (OUT OF SCOPE).** A completed apply operation that was not cancelled SHALL NOT be subject to a separate retroactive rollback; reverting an already-fully-applied, uncancelled operation is out of scope for the first release.

<a id="req-088"></a>**REQ-088 — Safe writes (SHALL).** The application SHALL preserve audio and unrelated metadata — including proprietary tag frames used by Rekordbox, Serato and Traktor (e.g. cue points, beatgrids, hot cues, GEOB blocks) — and SHALL NOT knowingly leave a partially written or corrupted audio file as a successful result.

<a id="req-130"></a>**REQ-130 — Minimal edits (SHALL).** WHEN the application writes edited fields to a file, it SHALL change only the tag data that stores those fields (and the equivalent fields of an existing ID3v1 tag); all other tag frames, their order and the audio data SHALL remain byte-identical. IF a file's tag cannot be parsed completely enough to guarantee this, THEN the application SHALL refuse to write it and report why, leaving the file unchanged.

<a id="req-089"></a>**REQ-089 — Recoverability (SHALL).** BEFORE exposing an audio file to write risk, the application SHALL preserve sufficient recovery material to restore its pre-write state; it SHALL verify successful completion before discarding that material (subject to the [REQ-085](#req-085) exception during an unresolved cancellation).

<a id="req-090"></a>**REQ-090 — No permanent write backup (SHALL).** The application SHALL NOT require retaining a full-file backup or separate pre-write metadata snapshot after the corresponding write has been safely completed, verified, and any cancellation decision resolved.

<a id="req-091"></a>**REQ-091 — Bounded temporary storage (SHALL).** The application SHALL bound temporary storage used by in-flight writes; the exact write technique, backup representation and concurrency level SHALL be determined during design and benchmarking.

<a id="req-092"></a>**REQ-092 — Interrupted apply (SHALL).** WHEN the application restarts after an interrupted apply, it SHALL reconcile actual file state, staged changes and recovery material; it SHALL NOT blindly reapply already successful edits or discard unsuccessful edits.

<a id="req-093"></a>**REQ-093 — Restoration (SHALL).** IF restoration is needed, THEN the application SHALL attempt safe automatic recovery. IF it cannot restore a track, THEN it SHALL retain the unresolved recovery state, prominently inform the user, provide available remedies, and prevent both editing and playback of the affected track while leaving other tracks usable.

<a id="req-094"></a>**REQ-094 — No forced resume (SHALL).** WHEN the application restarts after interrupted apply, it SHALL NOT automatically resume the unfinished batch or require a blocking resume dialog; the user SHALL be able to access remaining staging directly.

<a id="req-095"></a>**REQ-095 — Pending after apply (SHALL).** IF successfully applied tracks are pending, THEN the application SHALL offer one all-or-nothing choice to clear pending status for those tracks; clearing pending SHALL NOT be automatic.

<a id="req-096"></a>**REQ-096 — Result summary (SHALL).** WHEN an apply attempt finishes, the application SHALL show a nonblocking, revisit-able summary of successes and failures, grouped by reason, with affected files and actionable remedies.

<a id="req-097"></a>**REQ-097 — Summary replacement (SHALL).** WHEN another apply attempt completes, its summary SHALL replace the previous apply summary rather than accumulating a history in that view.

<a id="req-098"></a>**REQ-098 — Diagnostic detail (SHALL).** The application SHALL make sufficient error detail available for copying or export and later investigation, subject to the retention and privacy requirements in section 11.

## 9. Playback

<a id="req-099"></a>**REQ-099 — Single player (SHALL).** The application SHALL provide one global player for one track at a time, without a queue or automatic next-track playback.

<a id="req-100"></a>**REQ-100 — Explicit playback (SHALL).** WHEN a user explicitly loads a track into the player, playback SHALL begin immediately; merely selecting a row SHALL NOT start playback.

<a id="req-101"></a>**REQ-101 — Loading methods (SHALL).** The user SHALL be able to load a track through drag and drop, a quick track action, and a configurable shortcut.

<a id="req-102"></a>**REQ-102 — Loaded-track indication (SHALL).** The application SHALL identify the loaded track in relevant views and display at least its artist and title in the player.

<a id="req-103"></a>**REQ-103 — Controls (SHALL).** The player SHALL provide play, pause, restart, seek, duration, elapsed time, and an immediately accessible volume control.

<a id="req-104"></a>**REQ-104 — Looping (SHALL).** The player SHALL provide a one-click loop toggle. WHEN playback reaches the end, it SHALL restart if looping is enabled; otherwise it SHALL stop and return to the beginning.

<a id="req-105"></a>**REQ-105 — Editing during playback (SHALL).** WHEN metadata changes, playback SHALL continue without an interruption caused solely by the tag change. **[OPEN-K](#open-k):** applying to the playing track.

<a id="req-106"></a>**REQ-106 — Playback responsiveness (SHOULD).** Explicit playback SHOULD start within one second. Measurement: from the load action (drag and drop, quick action or shortcut) to first audible output, for a local MP3 and a local WAV on the minimum configuration defined in [REQ-010](#req-010); files on a cloud-sync mount are excluded.

<a id="req-107"></a>**REQ-107 — Additional player features (COULD).** The player COULD show remaining time, a waveform or support system media keys.

## 10. Settings, startup and shutdown

<a id="req-108"></a>**REQ-108 — Configurable shortcuts (SHALL).** The user SHALL be able to configure and persist action shortcuts.

<a id="req-109"></a>**REQ-109 — Shortcut uniqueness (SHALL).** IF a user assigns a shortcut already assigned to another configurable action, THEN the application SHALL prevent the conflicting assignment; a configurable shortcut SHALL map unambiguously to one action. **[OPEN-M](#open-m):** reserved keys and contexts.

<a id="req-110"></a>**REQ-110 — Persisted preferences (SHALL).** The application SHALL persist saved filters, per-view layout preferences and configured shortcuts across sessions.

<a id="req-111"></a>**REQ-111 — Quiet shutdown (SHALL).** WHEN the user closes the application with staged changes, it SHALL preserve them and allow closing without requiring an action, except as restricted by [REQ-086](#req-086). **[OPEN-I](#open-i):** closing during a running apply.

<a id="req-112"></a>**REQ-112 — Optional shutdown reminder (COULD).** The application COULD offer a dismissible reminder explaining that staged changes will be available next session, with a 'do not remind me again' option.

<a id="req-113"></a>**REQ-113 — Restored staging notice (SHALL).** WHEN startup restores staged changes, the application SHALL display a visible, nonblocking notification that dismisses automatically without requiring a click.

<a id="req-114"></a>**REQ-114 — Persistent attention cue (SHALL).** AFTER staged changes are restored, the staging entry point SHALL show a subtle indicator until the user opens the staging view for the first time in that session.

<a id="req-115"></a>**REQ-115 — Staging access (SHALL).** The user SHALL be able to open staging from the main window in one action.

## 11. Diagnostics and privacy

<a id="req-116"></a>**REQ-116 — Diagnostic log retention (SHALL).** The application SHALL retain diagnostic/error logs locally for 30 days and SHALL delete them after that period.

<a id="req-117"></a>**REQ-117 — No automatic transmission (SHALL).** The application SHALL NOT transmit diagnostic logs or error details to any external service automatically; any sharing or export of diagnostic data SHALL be a deliberate, user-initiated action.

<a id="req-118"></a>**REQ-118 — Minimal personal data in logs (SHOULD).** Diagnostic logs SHOULD avoid including more personal information (e.g. full user-folder paths) than is needed to identify the affected track and error.

<a id="req-119"></a>**REQ-119 — Crash reporting (COULD).** A future release COULD offer optional crash/diagnostic reporting to the developer. IF offered, THEN it SHALL be opt-in, require explicit consent before any data is transmitted, let the user review what would be sent, and remain disableable at any time.

## 12. Localization and text handling

<a id="req-123"></a>**REQ-123 — UI languages (SHALL).** The user interface SHALL be available in English and German. All user-facing text SHALL be translatable without code changes; product names (e.g. CrateEdit, Rekordbox) and the user's own metadata are not translated.

<a id="req-124"></a>**REQ-124 — Language selection (SHALL).** The application SHALL use the operating system's display language if it is supported, and English otherwise. A user-selectable language override SHOULD be offered in the settings; a language change MAY require a restart.

<a id="req-125"></a>**REQ-125 — Regional formatting (SHALL).** Dates, times and numbers shown in the user interface SHALL follow the user's regional settings.

<a id="req-126"></a>**REQ-126 — Accent- and case-insensitive search (SHALL).** Text search and text filters SHALL ignore letter case and diacritics, e.g. searching "beyonce" SHALL find "Beyoncé".

<a id="req-127"></a>**REQ-127 — Culture-aware sorting (SHALL).** Text columns SHALL be sorted according to the conventions of the user's regional settings rather than by character code, e.g. "Ärzte" SHALL sort near "Arzt", not after "Z".

<a id="req-128"></a>**REQ-128 — Unicode normalization (SHALL).** Text and file paths SHALL be compared in a way that treats canonically equivalent Unicode forms as equal (e.g. "é" as one character or as "e" plus a combining accent, as macOS file names use). The application SHALL NOT change the Unicode form of metadata values it does not edit.

<a id="req-129"></a>**REQ-129 — Language-independent data (SHALL).** Persisted data (index, staging, saved filters, shortcuts) and diagnostic logs SHALL NOT depend on the UI language, so that changing the language does not affect saved data and logs remain readable for troubleshooting.

<a id="req-131"></a>**REQ-131 — Fields not shown by DJ software (SHOULD).** WHEN the user edits a field that the user's DJ software does not read from the file's format (for Rekordbox: label, BPM and key of WAV files, see [ADR-0011](adr/0011-wav-tagging.md), and BPM of any file, since Rekordbox takes BPM only from its own analysis), the application SHOULD indicate that the change will be saved in the file but not shown by that software.

## 13. Deferred capabilities and unresolved decisions

<a id="req-120"></a>**REQ-120 — Future extensions (COULD).** Later versions COULD support multiple collection roots, manually curated named track lists, custom fields, external metadata providers and waveform display. These are not first-release acceptance criteria.

<a id="open-c"></a>**OPEN-C — Safe-write guarantees.** **Resolved** by [ADR-0010](adr/0010-safe-write-technique.md) after the [safe-write spike](spikes/2026-10-12-safe-write.md): temp file next to the original, local tag-only recovery material, verification before and after `File.Replace`, and reconciliation after a crash. Crash-atomic replace is only assumed on NTFS and APFS; FAT32, exFAT and Google Drive rely on the recovery material.

<a id="open-d"></a>**OPEN-D — WAV tagging.** **Resolved** by [ADR-0011](adr/0011-wav-tagging.md) after [probes in Rekordbox](spikes/2026-10-13-rekordbox-wav-probes.md): Rekordbox reads only the LIST/INFO chunk, as UTF-8, and ignores the "id3 " chunk. CrateEdit writes both: INFO for the fields it can hold, "id3 " for all fields.

<a id="open-f"></a>**OPEN-F — Move/rename identity for all tracks.** [REQ-020](#req-020) only attempts move matching for tracks with staged changes, while [REQ-040](#req-040) marks any unrecognized new file pending. Reorganizing a folder of finished tracks would therefore make them all pending and lose their date added. Proposal: recognize moves/renames for all tracks (same size and identical audio content appearing during the same sync), preserving identity, pending status and date added; [REQ-021](#req-021) stays strict for staged tracks. Also defines "unrecognized" in [REQ-040](#req-040).

<a id="open-g"></a>**OPEN-G — Revert failure ([REQ-084](#req-084)).** If a revert write fails, the file already holds the applied value, so keeping the original staged edit makes it a no-op, and the recovery material is discarded once the prompt is resolved, so the revert cannot be retried from it. Proposal: stage the revert itself (the pre-apply values) as an edit, so it can be applied later.

<a id="open-h"></a>**OPEN-H — Protection duration ([REQ-072](#req-072)).** Holding protection from validation through user decisions may block DJ software from reading those files for as long as the user deliberates. Decide whether protection starts at preflight or only when writing begins (with a re-check of on-disk state at that point).

<a id="open-i"></a>**OPEN-I — Closing during a running apply.** [REQ-111](#req-111) (quiet shutdown) and [REQ-086](#req-086) (blocked close during the cancellation prompt) do not cover a user-initiated close while an apply is running and not paused.

<a id="open-j"></a>**OPEN-J — Crash during the cancellation prompt.** Define what happens on restart when the application crashed or the OS shut down while the keep/revert/continue prompt was open (e.g. default to keep, or re-offer the choice while recovery material still exists).

<a id="open-k"></a>**OPEN-K — Applying to the playing track ([REQ-105](#req-105)).** Writing tags to the file currently loaded in the player may interrupt or break playback, depending on the write technique. Decide whether a brief interruption during apply is acceptable, or whether the playing track is deferred to the end of the run.

<a id="open-l"></a>**OPEN-L — Effective values.** Specify whether the collection table, search ([REQ-026](#req-026)), filters ([REQ-031](#req-031)) and suggestions show and match the staged value (if any) or the on-disk value. Proposal: staged value where present, marked per [REQ-038](#req-038).

<a id="open-m"></a>**OPEN-M — Reserved shortcuts ([REQ-109](#req-109)).** Define how configurable shortcuts interact with non-configurable keys (undo/redo, text-editing keys, edit traversal) and whether the same key may map to different actions in different contexts (table vs. editor).

<a id="open-n"></a>**OPEN-N — Date added for the initial scan ([REQ-035](#req-035)).** All tracks found in the initial scan would share the same date added, making the date-added sort meaningless for them. Proposal: seed date added from the filesystem creation time during the initial scan only; it is app-managed afterwards.
