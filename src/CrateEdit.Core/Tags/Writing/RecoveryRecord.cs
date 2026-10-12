namespace CrateEdit.Core.Tags.Writing;

/// <summary>
/// Everything needed to finish or undo a file write after a crash (<see href="https://github.com/thomaswening/CrateEdit/blob/main/docs/adr/0010-safe-write-technique.md">ADR-0010</see>, <see href="https://github.com/thomaswening/CrateEdit/blob/main/docs/requirements.md#req-089">REQ-089</see>, <see href="https://github.com/thomaswening/CrateEdit/blob/main/docs/requirements.md#req-092">REQ-092</see>).
/// The audio is never changed by a write, so the original file can be rebuilt from either
/// version of the file plus the original tags kept here.
/// </summary>
/// <param name="Id">Identifies the record in the store.</param>
/// <param name="TargetPath">The file being written.</param>
/// <param name="TempPath">The temporary file next to it.</param>
/// <param name="OriginalLength">Length of the original file, in bytes.</param>
/// <param name="OriginalId3v2Tag">The original ID3v2 tag bytes (empty if the file had none).</param>
/// <param name="OriginalId3v1Tag">The original ID3v1 tag, or null.</param>
/// <param name="OriginalAudioHash">SHA-256 of the original audio bytes.</param>
/// <param name="OriginalFileHash">SHA-256 of the whole original file.</param>
/// <param name="NewFileHash">
/// SHA-256 of the verified temporary file; null until the temporary file has been verified. Once
/// set, a target with this hash means the write completed.
/// </param>
public sealed record RecoveryRecord(
    Guid Id,
    string TargetPath,
    string TempPath,
    long OriginalLength,
    byte[] OriginalId3v2Tag,
    byte[]? OriginalId3v1Tag,
    byte[] OriginalAudioHash,
    byte[] OriginalFileHash,
    byte[]? NewFileHash);

/// <summary>Durable storage for recovery records, in local app data (<see href="https://github.com/thomaswening/CrateEdit/blob/main/docs/requirements.md#req-013">REQ-013</see>).</summary>
public interface IRecoveryStore
{
    /// <summary>Saves or replaces the record durably: it survives a crash right after this returns.</summary>
    void Save(RecoveryRecord record);

    void Delete(Guid id);

    IReadOnlyList<RecoveryRecord> LoadAll();
}
