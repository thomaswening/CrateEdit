# ADR-0004: SQLite in app data for all local state

- **Status:** Proposed
- **Date:** 2026-10-09

## Context
CrateEdit keeps substantial state of its own: the index of up to 20,000 tracks, staged changes, pending flags, date added, view preferences and recovery bookkeeping. Staging must be durable against crashes and power loss (REQ-053), startup from the index must be fast (REQ-015), and this state must survive the collection being disconnected (REQ-013, REQ-121).

## Decision
Store all local state in a single **SQLite** database in the app data folder, accessed through Microsoft.Data.Sqlite.

## Alternatives considered
- **JSON or other flat files:** simple, but no transactional updates, so durable staging would have to be built by hand, and loading 20,000 tracks is slower.
- **Storing app data in the collection (sidecar files or custom tags):** would travel with the collection, but pollutes the user's folders and tags, and is lost when the collection is unavailable.
- **An embedded document database (e.g. LiteDB):** workable, but SQLite is more widely used, better tooled and better understood.

## Consequences
- Transactions make durable staging and crash-safe bookkeeping straightforward.
- The index can be queried efficiently for sorting, searching and filtering.
- Schema changes need migrations from the first release onward.
- Status is Proposed until the Phase 2 index work confirms the startup target.
