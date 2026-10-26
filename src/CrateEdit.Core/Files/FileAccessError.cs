using CrateEdit.Core.Results;

namespace CrateEdit.Core.Files;

/// <summary>What went wrong when reading or writing a file.</summary>
public enum FileProblem
{
    /// <summary>Another program has the file open (e.g. a player or DJ software). Usually temporary.</summary>
    Locked,

    /// <summary>No permission, or the file is read-only.</summary>
    AccessDenied,

    /// <summary>The file, its folder or its drive does not exist (any more).</summary>
    NotFound,

    /// <summary>The drive is full.</summary>
    DiskFull,

    /// <summary>The written file did not read back as expected. The original was not replaced.</summary>
    VerificationFailed,

    /// <summary>Any other input/output failure.</summary>
    IoError,
}

/// <summary>
/// A file could not be read or written. Converted from .NET exceptions at the file-write
/// boundary (<see href="https://github.com/thomaswening/CrateEdit/blob/main/docs/adr/0009-result-pattern-and-typed-errors.md">ADR-0009</see>); the original exception is kept in <see cref="Error.Cause"/> for the log.
/// </summary>
public sealed record FileAccessError(FileProblem Problem, string Path) : Error
{
    /// <summary>For <see cref="FileProblem.Locked"/>: the programs holding the file, if known. Shown to the user.</summary>
    public IReadOnlyList<string> LockedBy { get; init; } = [];

    public override string Code => Problem switch
    {
        FileProblem.Locked => ErrorCodes.FilesLocked,
        FileProblem.AccessDenied => ErrorCodes.FilesAccessDenied,
        FileProblem.NotFound => ErrorCodes.FilesNotFound,
        FileProblem.DiskFull => ErrorCodes.FilesDiskFull,
        FileProblem.VerificationFailed => ErrorCodes.FilesVerificationFailed,
        FileProblem.IoError => ErrorCodes.FilesIoError,
        _ => throw new InvalidOperationException($"No error code for {Problem}."),
    };

    /// <summary>A locked file may be released soon, so retrying makes sense (<see href="https://github.com/thomaswening/CrateEdit/blob/main/docs/requirements.md#req-079">REQ-079</see>).</summary>
    public override bool IsTransient => Problem == FileProblem.Locked;

    /// <summary>Classifies an exception from a file operation on <paramref name="path"/>.</summary>
    public static FileAccessError From(Exception exception, string path)
    {
        var problem = exception switch
        {
            FileNotFoundException or DirectoryNotFoundException or DriveNotFoundException => FileProblem.NotFound,
            UnauthorizedAccessException => FileProblem.AccessDenied,
            IOException io when IsSharingViolation(io) => FileProblem.Locked,
            IOException io when IsDiskFull(io) => FileProblem.DiskFull,
            _ => FileProblem.IoError,
        };
        return new FileAccessError(problem, path) { Cause = exception };
    }

    // Windows reports its error code in the low 16 bits of HResult: 32 = sharing violation,
    // 33 = lock violation, 39 / 112 = disk full. On macOS the HResult carries errno: 28 = ENOSPC.
    private static bool IsSharingViolation(IOException e) => (e.HResult & 0xFFFF) is 32 or 33;

    private static bool IsDiskFull(IOException e) =>
        (e.HResult & 0xFFFF) is 39 or 112 || (!OperatingSystem.IsWindows() && e.HResult == 28);
}
