namespace CrateEdit.Core.Tags;

/// <summary>
/// The logical metadata fields CrateEdit reads and edits (<see href="https://github.com/thomaswening/CrateEdit/blob/main/docs/requirements.md#req-007">REQ-007</see>). Each field maps to a
/// format-specific tag frame, so the UI never deals with frame identifiers (<see href="https://github.com/thomaswening/CrateEdit/blob/main/docs/requirements.md#req-006">REQ-006</see>).
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
