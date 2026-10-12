# ADR-0009: Results with typed errors for expected failures

- **Status:** Proposed
- **Date:** 2026-10-12

## Context
CrateEdit has many expected failures: a tag that cannot be written safely, a file that changed externally, a missing file, a locked file. These failures have to be handled in three ways:

- **Shown to the user** in English or German (REQ-123), with a remedy where possible (REQ-096).
- **Grouped by reason** in the apply result summary (REQ-096).
- **Classified** as retryable or not (REQ-079).

The first tag code reported failures as English strings inside exceptions. Strings cannot be translated, grouped or classified reliably, and exceptions for expected outcomes make control flow hard to follow: nothing in a method's signature says it can fail.

C# has no built-in result type and no operator for early return on failure, so "everything returns a result" would add noise everywhere. .NET itself signals I/O problems with exceptions.

## Decision
Failures are handled in three categories:

| Category | Examples | Mechanism |
|---|---|---|
| **Expected domain outcome**: the caller must handle it | tag not writable, conflict detected, invalid user input | Return a `Result` / `Result<T>` carrying a typed `Error` |
| **Infrastructure failure**: caused by the environment | file locked, disk full, drive disconnected | .NET exceptions, caught **at a boundary** (e.g. per track in the apply pipeline) and converted into a typed `Error` there |
| **Programming error**: a bug | null argument, reading the value of a failed result | Exceptions that are not caught; they are logged and crash |

**Types** (`CrateEdit.Core.Results`):
- **`Error`:** an abstract record. Each concrete error has:
  - a stable, language-independent `Code` (e.g. `tags.malformed`);
  - `IsTransient`, used by retry logic;
  - typed details for logs (e.g. frame id, byte offset).

  The record's generated `ToString()` is what gets logged.
- **`Result` and `Result<T>`:** a success or an `Error`. Values and errors convert implicitly, so methods simply `return value;` or `return new SomeError(...);`. Callers check `IsFailure`, and the compiler then knows `Error` is not null. Reading `Value` from a failure throws, because it is a programming error. The types are written by hand, about 50 lines, with no library.
- **`ErrorCodes`:** the catalog of all codes. A new error code is added there, never as an ad-hoc string.

**Turning errors into behavior:**
- **User messages:** `ErrorMessages` in the app looks up the resource `Error_<code>` (dots and dashes replaced by underscores) in the current UI language. Messages contain no technical details. Tests fail if any code lacks an English or German message.
- **Logs:** errors are logged with all details, in English (REQ-129).
- **Retry and grouping:** the apply pipeline uses `IsTransient` and `Code` (Phase 6). Handlers are explicit components called at known places, not a global error bus.

**Rules for writing code:**
- A method whose failure is an expected outcome returns a `Result`; its documentation names the possible errors.
- Code that touches the file system lets exceptions pass until the nearest boundary that can act on them; it does not wrap every call.
- `catch (Exception)` is only allowed at boundaries, and must convert or log the exception.
- No `Result` for internal helpers where `null` or `bool` is clearer (e.g. `TryParse`).

## Alternatives considered
- **Exceptions for everything:** idiomatic in .NET, but expected outcomes become invisible in signatures, and exception messages are not suitable for translation or grouping.
- **Results for everything, including I/O:** consistent, but every file access would need wrapping, without language support for propagation.
- **A library (ErrorOr, FluentResults, OneOf):** workable, but our needs are small, and our own types fit the error-code catalog and the translation tests exactly.

## Consequences
- Expected failures are visible in method signatures and can be translated, grouped and classified by code.
- Each new error needs a code, an English and a German message, and (for apply) a retry classification. Tests enforce the messages.
- I/O exceptions still exist and must be converted at the right boundary. Phase 6 defines that boundary for the apply pipeline.
- First users: the tag reader (`Mp3Tags.WriteProblem`) and the tag writer (`Mp3TagWriter.Rewrite` returns `Result`), with `TagNotWritable` and its `TagProblem` kinds.
