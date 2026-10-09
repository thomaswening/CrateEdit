# ADR-0006: GPL-3.0 license

- **Status:** Accepted
- **Date:** 2026-10-09

## Context
CrateEdit should be free to use, distributed on GitHub at the user's own risk, and compatible with the licenses of its dependencies (MIT: Avalonia, Material.Avalonia, CommunityToolkit.Mvvm, SQLite; LGPL: TagLib#).

## Decision
License CrateEdit under the **GNU General Public License v3.0**.

## Alternatives considered
- **MIT:** fewer obligations for anyone reusing the code, but allows closed-source derivatives.

## Consequences
- Anyone distributing a modified version must publish its source under the GPL, so improvements stay open.
- The GPL's warranty disclaimer covers the "use at your own risk" requirement.
- LGPL dependencies must be shipped unmodified and replaceable; MIT dependencies need their notices included in releases.
