namespace CrateEdit.Core.Results;

/// <summary>
/// An expected failure that a caller must handle (ADR-0009). Each error has a stable
/// <see cref="Code"/> that the UI translates into a localized message and the apply pipeline uses
/// to decide on retries and to group failures. The record's generated <c>ToString()</c> includes
/// all details and is what gets logged.
/// </summary>
public abstract record Error
{
    /// <summary>Stable, language-independent identifier, e.g. "tags.malformed". Listed in <see cref="ErrorCodes"/>.</summary>
    public abstract string Code { get; }

    /// <summary>True if trying again later may succeed (e.g. a file briefly locked by another program).</summary>
    public virtual bool IsTransient => false;

    /// <summary>
    /// The exception this error was converted from, if any (e.g. an <see cref="IOException"/>
    /// caught at the apply boundary). Kept for diagnostics: it is logged, never shown to the user.
    /// </summary>
    public Exception? Cause { get; init; }
}
