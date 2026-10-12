# CrateEdit: Development Process
Draft | 9 October 2026

## Branching
GitHub flow:
- `main` is always releasable and only changes through pull requests.
- Work happens on short-lived branches named `<type>/<short-description>`, e.g. `feat/tag-layer`, `fix/scan-progress`.
- PRs are squash-merged, so each PR becomes exactly one commit on `main`.
- No long-lived `develop` or release branches; releases are tags on `main`.

## Commits and changelog
- Commits follow [Conventional Commits](https://www.conventionalcommits.org/): `feat:`, `fix:`, `test:`, `docs:`, `refactor:`, `perf:`, `build:`, `ci:`, `chore:`. Breaking changes use `!` or a `BREAKING CHANGE:` footer.
- Because PRs are squash-merged, the **PR title** becomes the commit message on `main`; CI validates that it is a conventional commit.
- A scope may name the area, e.g. `feat(tags): write BPM and key to WAV`.
- [release-please](https://github.com/googleapis/release-please) reads the conventional commits on `main`, maintains a release PR with the next version and `CHANGELOG.md`, and creates the GitHub release and tag when that PR is merged.
- Versioning follows SemVer, starting at `0.x` until the requirements are complete. Before 1.0, `feat` bumps the minor version and `fix` the patch version. The version lives in `Directory.Build.props` and is updated only by release-please.
- The release PR is kept open and updated automatically; it is merged only when a release is intended (the first one at the end of Milestone 2).
- `feat`, `fix`, `perf` and `docs` appear in the changelog; `build`, `ci`, `test`, `refactor` and `chore` are hidden.

## Pull requests
- One PR per coherent, reviewable slice of a phase (typically several PRs per phase), not one PR per phase.
- Every PR states which requirement IDs it implements or touches.
- A PR merges only when CI is green on Windows and macOS and the owner has reviewed it.

## Error handling
Follow [ADR-0009](adr/0009-result-pattern-and-typed-errors.md):
- **Expected failures** return `Result` / `Result<T>` with a typed `Error`.
- **I/O exceptions** are converted into errors at boundaries.
- **Bugs** throw and are not caught.

Every new error code goes into `ErrorCodes` and needs an `Error_<code>` message in both resource files in the same PR; tests enforce this.

## User-facing text
- No hard-coded UI text: every string goes into `src/CrateEdit.App/Resources/Strings.resx` (English) **and** `Strings.de.resx` (German) in the same PR. Tests fail on missing translations and on literal text in XAML.
- Text comparisons (search, sorting, equality of names and paths) use `CrateEdit.Core.Text.TextMatching`, never ad-hoc `string.Equals` or `Contains`.

## Requirement links
Every mention of a requirement or open decision in the docs links to its definition in [requirements.md](requirements.md), e.g. `[REQ-088](requirements.md#req-088)`. New requirements get an anchor (`<a id="req-131"></a>`) at the start of their definition. You don't need to write either by hand: run

```bash
dotnet run tools/RequirementLinks.cs -- --fix
```

and it adds missing anchors and links. CI runs the same tool with `--check` and fails on unlinked mentions, links to unknown requirements, and mentions of requirements that do not exist.

## Architecture documentation
- [architecture.md](architecture.md) is kept current: a PR that adds or changes a building block, dependency rule or key concept updates it in the same PR.
- Significant decisions (technology choices, data formats, safety mechanisms, anything hard to reverse) get an [Architecture Decision Record](adr/) in the PR that makes them. A changed decision gets a new ADR that supersedes the old one.

## Testing and guardrails
Each requirement that can be verified automatically gets at least one test tagged with its ID, e.g. `[Trait("Req", "REQ-088")]`. Tests are added in the same PR as the feature.

| Layer | What it covers | Examples |
|---|---|---|
| Unit | Core logic without I/O | staging model, undo grouping, retry classification, effective values |
| Integration | Real files and storage in temp directories | tag read/write round trips, byte-level preservation of audio and foreign frames (GEOB, cues), SQLite index, file watching, crash/fault injection during writes and recovery |
| Architecture | Structural rules on the code itself | only the apply component may reference the tag writer, so no other code path can write audio files ([REQ-052](requirements.md#req-052), [REQ-075](requirements.md#req-075)) |
| End-to-end | The real app UI driven headlessly via Avalonia.Headless | keyboard workflow, staging then apply, no autoplay on selection, prompts and their choices |

Key guardrails and how they are enforced:
- **No write outside apply** ([REQ-052](requirements.md#req-052), [REQ-075](requirements.md#req-075)): architecture test plus an integration test that edits, stages and undoes on real files and asserts their bytes are unchanged.
- **Preserve everything not edited** ([REQ-088](requirements.md#req-088)): byte-level comparison of all non-edited frames and the audio payload after every write test.
- **Never leave a corrupt file** ([REQ-088](requirements.md#req-088), [REQ-089](requirements.md#req-089), [REQ-092](requirements.md#req-092)): fault-injection tests that abort the write at each step and verify the file is either fully old or fully new and that recovery reconciles correctly.
- **No silent overwrite** ([REQ-074](requirements.md#req-074)): tests that change a file externally between staging and apply and assert it is skipped or flagged.
- **No automatic transmission** ([REQ-117](requirements.md#req-117)): architecture test that the app has no network-capable dependency in the diagnostics path.

### Test fixtures
Real DJ-software-tagged files contain copyrighted audio and must not be committed. Fixtures in the repository are generated: short synthetic audio (silence or a tone) carrying tag frames copied from real files (Serato GEOB, Rekordbox/Traktor data), so the frames being protected are real while the audio is not. Original sample files stay outside the repository.

Tests that need real files (e.g. `RealFileTests`) read them from the folder in the `CRATEEDIT_SAMPLES` environment variable, always work on temporary copies, and are skipped when the variable is not set (as in CI). Run them locally before merging changes to tag reading or writing:
```bash
CRATEEDIT_SAMPLES=/path/to/sample/mp3s dotnet test -c Release --filter "FullyQualifiedName~RealFile"
```

### Requirements traceability
- CI produces a traceability report listing every requirement, the tests tagged with it, and a status: **Passing**, **Failing**, **Manual** or **Not covered**. It appears in each CI job's summary and in the `test-results-<os>` artifact.
- The report and a unit test both fail on tags that name a requirement that does not exist.
- To produce the report locally:
  ```bash
  dotnet build -c Release
  dotnet tests/CrateEdit.Tests/bin/Release/net10.0/CrateEdit.Tests.dll -xml TestResults/results.xml
  dotnet run tools/Traceability.cs -- --requirements docs/requirements.md --manual docs/manual-verification.md --results TestResults/results.xml --output TestResults/traceability.md
  ```
- Requirements that cannot be automated (e.g. "Rekordbox reads the written key", performance on the minimum configuration) are listed in `docs/manual-verification.md` with steps, and checked manually.
- **Phase exit check:** a phase is complete only when every requirement assigned to it in the roadmap is either covered by passing tests or listed and checked in the manual verification list, and the phase's open decisions are recorded in the requirements.

## Continuous integration
GitHub Actions on every PR and on `main`:
- build and all test layers on Windows and macOS,
- PR title check (conventional commit),
- traceability report in the job summary and as a build artifact.

On `main`, the release-please workflow maintains the release PR. Building unsigned Windows and macOS packages and attaching them to the GitHub release is added before the first release (Milestone 2).

## Releases and distribution
- Releases are published on GitHub Releases, unsigned. Windows SmartScreen and macOS Gatekeeper will warn on first launch; the release notes explain how to open the app anyway.
- The README and every release state that the software is provided as is and used at your own risk.
- The first release is planned at the end of Milestone 2 (see [roadmap.md](roadmap.md)).
