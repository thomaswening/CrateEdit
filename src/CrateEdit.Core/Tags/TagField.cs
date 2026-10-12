namespace CrateEdit.Core.Tags;

/// <summary>
/// The logical metadata fields CrateEdit reads and edits (REQ-007). Each field maps to a
/// format-specific tag frame, so the UI never deals with frame identifiers (REQ-006).
/// </summary>
public enum TagField
{
    Artist,
    Title,
    Album,
    Genre,
    Label,
    Bpm,
    Key,
    Comment,
    Year,
}
