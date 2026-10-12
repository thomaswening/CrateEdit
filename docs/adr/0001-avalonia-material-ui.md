# ADR-0001: Avalonia with Material.Avalonia for the UI

- **Status:** Accepted
- **Date:** 2026-09-25

## Context
CrateEdit must run on Windows and macOS (many DJs use macOS). The maintainer knows .NET and WPF, which is Windows-only. The core UI is a large, editable, keyboard-driven table (20,000 rows, [REQ-005](../requirements.md#req-005), [REQ-046](../requirements.md#req-046), [REQ-049](../requirements.md#req-049)).

## Decision
Use **Avalonia** for the UI, styled with **Material.Avalonia**, using MVVM with CommunityToolkit.Mvvm.

## Alternatives considered
- **WPF:** Windows-only.
- **.NET MAUI:** reaches macOS only through Mac Catalyst, has weak desktop support and no good editable data grid.
- **Uno Platform:** viable, but a smaller desktop ecosystem than Avalonia.
- **Tauri or Electron with a web UI:** good tables, but requires a second language and stack for a single maintainer.

## Consequences
- XAML, bindings, styles and MVVM carry over from WPF, so existing knowledge is reused.
- The UI runs natively on both platforms from one codebase, and can be tested headlessly in CI (Avalonia.Headless).
- Avalonia's grid and macOS keyboard behavior differ in details from WPF; this is checked early with the 20,000-row table in Phase 2.
- Material.Avalonia adds a consistent visual style and light/dark support, but it is a third-party dependency whose releases must keep up with Avalonia's.
