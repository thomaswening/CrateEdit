# ADR-0002: .NET 10 with a UI-independent core project

- **Status:** Accepted
- **Date:** 2026-10-09

## Context
The most critical logic (staging, conflict detection, safe writes, recovery) must be tested thoroughly and automatically on every change, independent of any UI. The project should be easy to build for a single maintainer on Windows and macOS.

## Decision
- Target **.NET 10** (the current LTS release), pinned in `global.json`.
- Split the solution into `CrateEdit.Core` (all domain logic, no UI dependency), `CrateEdit.App` (Avalonia UI, MVVM) and `CrateEdit.Tests`.
- Manage package versions centrally (`Directory.Packages.props`), enable nullable reference types and treat warnings as errors.
- Start with one Core project organized by namespaces; split a block into its own project only when a harder boundary is needed.

## Alternatives considered
- **Separate projects per block from the start:** stronger boundaries, but more ceremony before the boundaries are known. Architecture tests enforce the important rules in the meantime.
- **Logic in view models:** faster at first, but makes the safety-critical logic hard to test.

## Consequences
- Core logic is testable with plain unit and integration tests; UI behavior with headless end-to-end tests.
- Dependency rules (e.g. only the apply pipeline may write tags) must be enforced by architecture tests rather than project references.
