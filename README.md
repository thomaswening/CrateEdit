# CrateEdit

[![CI](https://github.com/thomaswening/CrateEdit/actions/workflows/ci.yml/badge.svg)](https://github.com/thomaswening/CrateEdit/actions/workflows/ci.yml)
[![License: GPL v3](https://img.shields.io/badge/license-GPL--3.0-blue.svg)](LICENSE)
![Platforms: Windows | macOS](https://img.shields.io/badge/platforms-Windows%20%7C%20macOS-lightgrey.svg)
![.NET 10](https://img.shields.io/badge/.NET-10-512BD4.svg)

**A tag editor for DJ music collections that never touches your cue points.**

CrateEdit is for DJs who keep their metadata clean: artist, title, album, genre, label, BPM, key, comment and year, across thousands of MP3 and WAV files. You edit in a fast, keyboard-driven table; changes are staged first and only written when you apply them. Every write is checked, crash-safe, and leaves everything it does not edit, from audio to Serato, Traktor and Rekordbox data, exactly as it was.

> **Status: in development.** The tag engine is done and tested; the application UI comes next. There is no release yet. See the [roadmap](docs/roadmap.md).

## What already works

The core that reads and writes your files ([Phase 1](docs/roadmap.md)):

- **MP3 (ID3v2.3 / 2.4) and WAV.** CrateEdit uses its own tag editor, which rebuilds only the frames you edit and copies every other byte unchanged ([why not a tag library](docs/spikes/2026-10-12-tag-library-preservation.md)).
- **Crash-safe writes.** Every write goes through a temporary file, is verified before and after it replaces the original, and keeps enough recovery data to finish or undo it after a crash or power loss, also on USB sticks and Google Drive ([how](docs/adr/0010-safe-write-technique.md)).
- **Refuses rather than guesses.** A file whose tags it does not fully understand is left untouched, with an explanation.
- **English and German**, with search that ignores case and accents ("beyonce" finds "Beyoncé").

## Verified with DJ software

Files edited by CrateEdit were checked by hand in the DJ applications ([details](docs/manual-verification.md)):

| | Rekordbox | Serato | Traktor |
|---|---|---|---|
| Edited title, artist, genre and comment shown | ✓ MP3, WAV | ✓ MP3, WAV | ✓ MP3, WAV |
| Edited album and year shown | ✓ MP3, WAV | not tested | not tested |
| Edited label and key shown | ✓ MP3¹ | not tested | not tested |
| Hot cues, loops and beatgrid kept | n/a² | ✓ | ✓ |

¹ Rekordbox does not read label and key from WAV files, and never takes BPM from tags; CrateEdit will point this out when you edit those fields ([details](docs/adr/0011-wav-tagging.md)).
² Rekordbox keeps cue points in its own library, not in the files.

## Planned

The full scope is in the [requirements](docs/requirements.md); in short:

- A collection table for up to 20,000 tracks that opens in seconds, with saved filters and a "pending" workflow for new tracks.
- Inline and batch editing with keyboard shortcuts, suggestions and undo.
- A review step before changes are written, with detection of changes made by other programs in the meantime.
- A built-in player for checking tracks while tagging.

## Use at your own risk

CrateEdit writes to your audio files. It is provided as is, without warranty of any kind. Back up your collection before using it. Releases will not be code-signed, so Windows and macOS will warn on first launch.

## Building from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/thomaswening/CrateEdit.git
cd CrateEdit
dotnet build
dotnet test
dotnet run --project src/CrateEdit.App
```

## Documentation

| Document | Contents |
|---|---|
| [Requirements](docs/requirements.md) | What CrateEdit must do, as numbered, verifiable requirements |
| [Architecture](docs/architecture.md) | How it is built and why, with [decision records](docs/adr/) |
| [Roadmap](docs/roadmap.md) | Phases and milestones |
| [Development process](docs/development.md) | Branching, commits, tests and guardrails |
| [ID3](docs/formats/id3.md) and [WAV](docs/formats/wav.md) guides | How tags are stored, byte by byte |

## License

CrateEdit is licensed under the [GNU General Public License v3.0](LICENSE).
