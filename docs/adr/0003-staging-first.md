# ADR-0003: All edits are staged; files are written only on apply

- **Status:** Accepted
- **Date:** 2026-10-09

## Context
Writing tags directly on every edit would put the user's collection at risk with every keystroke, make undo depend on rewriting files, and spread write logic across the application. DJ software also reads and writes the same files, so writes must be coordinated and checked.

## Decision
Every accepted edit is stored as a **staged change** in local app data ([REQ-052](../requirements.md#req-052)). Audio files are written only when the user applies staged changes ([REQ-075](../requirements.md#req-075)), and only by the apply pipeline. There is no direct-write mode in the first release.

## Alternatives considered
- **Direct writes with optional staging:** simpler for small edits, but two code paths for writing, and edits could not be reviewed or undone cheaply.
- **Write immediately, keep backups for undo:** every edit becomes a risky file operation, and backups of large WAV files are expensive.

## Consequences
- Editing, undo and review are fast and never touch audio files.
- All write risk is concentrated in one pipeline that can be protected, tested and fault-injected thoroughly.
- Staged changes must themselves be durable ([REQ-053](../requirements.md#req-053)), and the app must handle staged changes for files that later go missing or change externally.
- The user has an extra step (apply), which [REQ-055](../requirements.md#req-055) keeps lightweight by not requiring a review first.
