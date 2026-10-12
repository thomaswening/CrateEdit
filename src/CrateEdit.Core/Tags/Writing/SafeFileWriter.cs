using CrateEdit.Core.Files;
using CrateEdit.Core.Results;
using Serilog;

namespace CrateEdit.Core.Tags.Writing;

/// <summary>The steps of a safe write (<see href="https://github.com/thomaswening/CrateEdit/blob/main/docs/adr/0010-safe-write-technique.md">ADR-0010</see>), in order.</summary>
public enum WriteStep
{
    /// <summary>1. Recovery material for the original is stored durably.</summary>
    RecoveryRecorded,

    /// <summary>2. The temporary file with the new content is written and flushed.</summary>
    TempWritten,

    /// <summary>3. The temporary file was read back and verified; its hash is recorded.</summary>
    TempVerified,

    /// <summary>4. The temporary file replaced the original.</summary>
    Replaced,

    /// <summary>5. The result was verified at the original path.</summary>
    ResultVerified,
}

/// <summary>
/// Writes tag changes to an MP3 file without ever leaving it half-written or lost (<see href="https://github.com/thomaswening/CrateEdit/blob/main/docs/adr/0010-safe-write-technique.md">ADR-0010</see>,
/// <see href="https://github.com/thomaswening/CrateEdit/blob/main/docs/requirements.md#req-088">REQ-088</see>, <see href="https://github.com/thomaswening/CrateEdit/blob/main/docs/requirements.md#req-089">REQ-089</see>). The original is only replaced by a fully written and verified temporary
/// file, and recovery material is kept until the result is verified, so
/// <see cref="RecoveryReconciler"/> can finish or undo the write after a crash.
/// </summary>
public sealed class SafeFileWriter(IRecoveryStore recoveryStore)
{
    private static readonly ILogger Logger = Log.ForContext<SafeFileWriter>();

    /// <summary>Test hook: called after each step. Throwing from it simulates a crash at that point.</summary>
    internal Action<WriteStep>? AfterStep { get; init; }

    /// <summary>Applies <paramref name="changes"/> to the audio file at <paramref name="path"/>, in any supported format.</summary>
    /// <returns>
    /// Success, or one of these errors, in which case the original file is unchanged:
    /// <see cref="TagNotWritable"/> if the tags cannot be written safely;
    /// <see cref="FileAccessError"/> if the file could not be read, written or verified;
    /// <see cref="RecoveryUnavailable"/> if the recovery material could not be stored.
    /// </returns>
    public Result Write(string path, IReadOnlyDictionary<TagField, string?> changes)
    {
        if (TagFileFormats.For(path) is not { } format)
        {
            return new TagNotWritable(TagProblem.UnsupportedFormat);
        }

        var write = new PendingWrite(path, TempPathFor(path), changes, format);
        try
        {
            if (RecordRecovery(write) is { IsFailure: true } notRecorded)
            {
                return notRecorded;
            }

            AfterStep?.Invoke(WriteStep.RecoveryRecorded);

            if (WriteTemp(write) is { IsFailure: true } notWritten)
            {
                Discard(write);
                return notWritten;
            }

            AfterStep?.Invoke(WriteStep.TempWritten);

            if (VerifyTemp(write) is { IsFailure: true } notVerified)
            {
                Discard(write);
                return notVerified;
            }

            AfterStep?.Invoke(WriteStep.TempVerified);

            Replace(write);
            AfterStep?.Invoke(WriteStep.Replaced);

            if (VerifyResult(write) is { IsFailure: true } wrongResult)
            {
                return wrongResult;
            }

            AfterStep?.Invoke(WriteStep.ResultVerified);

            DeleteRecoveryMaterial(write);
            return Result.Success;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A file operation on the audio file or the temporary file failed. The original may
            // or may not have been replaced; reconciliation tells which and either finishes the
            // write or keeps the original.
            TryReconcile(write);
            return ToFileError(exception, path);
        }
    }

    /// <summary>The temporary file used for <paramref name="path"/>: hidden-style name in the same folder.</summary>
    public static string TempPathFor(string path) =>
        Path.Combine(Path.GetDirectoryName(path)!, "." + Path.GetFileName(path) + ".crateedit-tmp");

    /// <summary>Step 1: store everything needed to rebuild the original, before anything is written.</summary>
    private Result RecordRecovery(PendingWrite write)
    {
        write.Original = write.Format.ReadLayout(write.Path);
        write.OriginalValues = write.Format.Read(write.Path).Values;
        var record = new RecoveryRecord(Guid.NewGuid(), write.Path, write.TempPath, write.Original.Length,
            write.Original.Prefix, write.Original.Suffix, write.Original.AudioHash, write.Original.FileHash,
            NewFileHash: null);

        if (SaveRecord(record) is { IsFailure: true } notSaved)
        {
            return notSaved;
        }

        write.Record = record;
        return Result.Success;
    }

    /// <summary>Step 2: write the complete new file next to the original.</summary>
    private static Result WriteTemp(PendingWrite write)
    {
        if (File.Exists(write.TempPath))
        {
            // Left over from an earlier interrupted write of this file; its record was resolved.
            File.Delete(write.TempPath);
        }

        return write.Format.Rewrite(write.Path, write.TempPath, write.Changes);
    }

    /// <summary>
    /// Step 3: the temporary file must hold exactly the intended values, all other field values
    /// unchanged, and the identical audio. Its hash is then recorded, so reconciliation can
    /// recognize it.
    /// </summary>
    private Result VerifyTemp(PendingWrite write)
    {
        var original = write.Original!;
        var temp = write.Format.ReadLayout(write.TempPath);
        var tags = write.Format.Read(write.TempPath);

        var isExpected = temp.AudioHash.AsSpan().SequenceEqual(original.AudioHash)
            && temp.AudioLength == original.AudioLength
            && write.Format.IsConsistentRewrite(original, temp)
            && tags.CanWrite
            && Enum.GetValues<TagField>().All(field => tags.Values[field] == ExpectedValue(write, field));
        if (!isExpected)
        {
            return new FileAccessError(FileProblem.VerificationFailed, write.Path);
        }

        write.NewFileHash = temp.FileHash;
        var record = write.Record! with { NewFileHash = temp.FileHash };
        if (SaveRecord(record) is { IsFailure: true } notSaved)
        {
            return notSaved;
        }

        write.Record = record;
        return Result.Success;
    }

    /// <summary>Step 4: keeps the creation date and attributes on Windows; an atomic rename on macOS.</summary>
    private static void Replace(PendingWrite write) =>
        File.Replace(write.TempPath, write.Path, destinationBackupFileName: null);

    /// <summary>Step 5: the original path must now hold exactly the verified temporary file.</summary>
    private Result VerifyResult(PendingWrite write)
    {
        if (AudioLayout.HashFile(write.Path).AsSpan().SequenceEqual(write.NewFileHash))
        {
            return Result.Success;
        }

        // Not what was verified: let reconciliation restore the original.
        TryReconcile(write);
        return new FileAccessError(FileProblem.VerificationFailed, write.Path);
    }

    private static string? ExpectedValue(PendingWrite write, TagField field) =>
        write.Changes.TryGetValue(field, out var changed)
            ? (string.IsNullOrEmpty(changed) ? null : changed)
            : write.OriginalValues![field];

    private Result SaveRecord(RecoveryRecord record)
    {
        try
        {
            recoveryStore.Save(record);
            return Result.Success;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new RecoveryUnavailable { Cause = exception };
        }
    }

    /// <summary>The write succeeded; failing to clean up must not turn it into a failure.</summary>
    private void DeleteRecoveryMaterial(PendingWrite write)
    {
        try
        {
            recoveryStore.Delete(write.Record!.Id);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The leftover record is resolved as "write completed" at the next start.
            Logger.Warning(exception, "Could not delete recovery record {RecordId} after a successful write", write.Record!.Id);
        }
    }

    /// <summary>Removes the temporary file and the record of a write that never touched the original.</summary>
    private void Discard(PendingWrite write)
    {
        try
        {
            if (File.Exists(write.TempPath))
            {
                File.Delete(write.TempPath);
            }

            if (write.Record is { } record)
            {
                recoveryStore.Delete(record.Id);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Harmless: reconciliation at the next start sees the intact original and cleans up.
            Logger.Warning(exception, "Could not clean up after a discarded write to {Path}", write.Path);
        }
    }

    private void TryReconcile(PendingWrite write)
    {
        if (write.Record is not { } record)
        {
            return;
        }

        try
        {
            RecoveryReconciler.Reconcile(record, recoveryStore);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // E.g. the file is still locked. The record stays and is resolved at the next start.
            Logger.Warning(exception, "Could not reconcile the write to {Path} now", write.Path);
        }
    }

    private static FileAccessError ToFileError(Exception exception, string path)
    {
        var error = FileAccessError.From(exception, path);
        return error.Problem == FileProblem.Locked ? error with { LockedBy = LockingProcesses.Find(path) } : error;
    }

    /// <summary>The state of one write as it moves through the steps.</summary>
    private sealed class PendingWrite(string path, string tempPath, IReadOnlyDictionary<TagField, string?> changes, ITagFileFormat format)
    {
        public string Path { get; } = path;

        public string TempPath { get; } = tempPath;

        public IReadOnlyDictionary<TagField, string?> Changes { get; } = changes;

        public ITagFileFormat Format { get; } = format;

        public AudioLayout? Original { get; set; }

        public TagValues? OriginalValues { get; set; }

        public RecoveryRecord? Record { get; set; }

        public byte[]? NewFileHash { get; set; }
    }
}
