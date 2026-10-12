# ADR-0007: Localization with resource files and central text-matching rules

- **Status:** Accepted
- **Date:** 2026-10-12

## Context
The UI must be available in English and German (REQ-123), following the operating system's language with English as fallback (REQ-124). Independently of the UI language, a DJ collection is full of names with accents and umlauts, and macOS stores file names in decomposed Unicode form, so search, sorting and identity checks must handle this correctly (REQ-126 to REQ-128). Retrofitting either concern after many views and features exist is expensive.

## Decision
**UI text:**
- All user-facing text lives in `CrateEdit.App/Resources/Strings.resx` (English, the neutral language) with one satellite file per translation (`Strings.de.resx`). A strongly typed `Strings` class is generated at build time.
- XAML references strings with `{x:Static res:Strings.Key}`; code uses `Strings.Key`. Product names and user metadata are never translated.
- .NET's resource lookup uses the current UI culture, which defaults to the OS display language, and falls back to English (`NeutralResourcesLanguage("en")`).
- Because `x:Static` reads values once, a language change takes effect after a restart (permitted by REQ-124).
- Command display names come from resources; command ids, shortcuts and all persisted data are language-independent (REQ-129). Diagnostic logs are always written with invariant formatting.

**Text matching:**
- All comparisons of user-visible text go through `CrateEdit.Core.Text.TextMatching`:
  - **Search:** culture-invariant, ignoring case and diacritics, so results do not change with the UI language (REQ-126).
  - **Sorting:** culture-aware by the user's regional settings, ignoring case (REQ-127).
  - **Identity:** ordinal comparison after NFC normalization, so composed and decomposed forms are equal while case and accents still matter (REQ-128).
- Normalization is used for comparison only; values are never rewritten in files.

## Alternatives considered
- **English-only UI, localize later:** cheapest now, but every later view would have to be revisited.
- **Runtime-switchable localization (a binding-based translation service):** allows switching without restart, but adds a custom mechanism to every text binding. A restart is acceptable for a setting that rarely changes.
- **JSON or other custom translation files:** possible, but `.resx` is the .NET standard with build-time code generation, satellite assemblies and tooling support.
- **Ordinal comparisons everywhere:** simple and fast, but "beyonce" would not find "Beyoncé", umlauts would sort after "Z", and macOS file names would not match their Windows-form equivalents.

## Consequences
- Adding a language means adding one `.resx` file; tests fail if any string is missing or empty in a translation, or if XAML contains hard-coded text.
- Every new UI string requires an English and a German entry in the same PR.
- Search and sorting depend on ICU, which .NET uses on all supported platforms; invariant globalization mode must not be enabled.
- Later phases (table search, filters, move detection) must use `TextMatching` instead of ad-hoc comparisons.
