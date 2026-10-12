using CrateEdit.Core.Files;
using CrateEdit.Core.Results;

namespace CrateEdit.Core.Tags.Writing;

/// <summary>The steps of a safe write (ADR-0010), in order.</summary>
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
/// Writes tag changes to an MP3 file without ever leaving it half-written or lost (ADR-0010,
/// REQ-088, REQ-089). The original is only replaced by a fully written and verified temporary
/// file, and recovery material is kept until the result is verified, so
/// <see cref="RecoveryReconciler"/> can finish or undo the write after a crash.
/// </summary>
public sealed class SafeFileWriter(IRecoveryStore recoveryStore)
{
    /// <summary>Test hook: called after each step. Throwing from it simulates a crash at that point.</summary>
    internal Action<WriteStep>? AfterStep { get; init; }

    /// <summary>
    /// Applies <paramref name="changes"/> to the file at <paramref name="path"/>.
    /// </summary>
    /// <returns>
    /// Success; <see cref="TagNotWritable"/> if the tags cannot be written safely; or
    /// <see cref="FileAccessError"/> if the file could not be read, written or verified. In every
    /// failure case the original file is unchanged.
    /// </returns>
    public Result Write(string path, IReadOnlyDictionary<TagField, string?> changes)
    {
        var tempPath = TempPathFor(path);
        RecoveryRecord? record = null;
        try
        {
            // 1. Recovery material.
            var original = Mp3Layout.Read(path);
            var originalValues = Mp3TagReader.Read(path).Values;
            record = new RecoveryRecord(Guid.NewGuid(), path, tempPath, original.Length, original.Id3v2Bytes,
                original.Id3v1Bytes, original.AudioHash, original.FileHash, NewFileHash: null);
            recoveryStore.Save(record);
            AfterStep?.Invoke(WriteStep.RecoveryRecorded);

            // 2. Temporary file next to the original.
            DeleteIfExists(tempPath);
            var written = Mp3TagWriter.Rewrite(path, tempPath, changes);
            if (written.IsFailure)
            {
                Discard(record);
                return written.Error;
            }

            AfterStep?.Invoke(WriteStep.TempWritten);

            // 3. Verify the temporary file before it replaces anything.
            var temp = Mp3Layout.Read(tempPath);
            if (!IsExpectedContent(tempPath, temp, original, originalValues, changes))
            {
                Discard(record);
                return new FileAccessError(FileProblem.VerificationFailed, path);
            }

            record = record with { NewFileHash = temp.FileHash };
            recoveryStore.Save(record);
            AfterStep?.Invoke(WriteStep.TempVerified);

            // 4. Replace. Keeps the creation date and attributes on Windows; an atomic rename on macOS.
            File.Replace(tempPath, path, destinationBackupFileName: null);
            AfterStep?.Invoke(WriteStep.Replaced);

            // 5. Verify the result, then the recovery material is no longer needed.
            if (!Mp3Layout.HashFile(path).AsSpan().SequenceEqual(temp.FileHash))
            {
                // Not what was verified: let reconciliation restore the original.
                RecoveryReconciler.Reconcile(record, recoveryStore);
                return new FileAccessError(FileProblem.VerificationFailed, path);
            }

            AfterStep?.Invoke(WriteStep.ResultVerified);
            recoveryStore.Delete(record.Id);
            return Result.Success;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            if (record is not null)
            {
                // The original may or may not have been replaced; reconciliation knows how to
                // tell and either finishes the write or keeps the original. If it cannot run now
                // (e.g. the file is still locked), the record stays for the next startup.
                try
                {
                    RecoveryReconciler.Reconcile(record, recoveryStore);
                }
                catch (Exception retry) when (retry is IOException or UnauthorizedAccessException)
                {
                }
            }

            return FileAccessError.From(exception, path);
        }
    }

    /// <summary>The temporary file used for <paramref name="path"/>: hidden-style name in the same folder.</summary>
    public static string TempPathFor(string path) =>
        Path.Combine(Path.GetDirectoryName(path)!, "." + Path.GetFileName(path) + ".crateedit-tmp");

    /// <summary>
    /// The temporary file must hold exactly the intended values, all other field values unchanged,
    /// the identical audio, and an ID3v1 tag only if the original had one.
    /// </summary>
    private static bool IsExpectedContent(string tempPath, Mp3Layout temp, Mp3Layout original, TagValues originalValues,
        IReadOnlyDictionary<TagField, string?> changes)
    {
        if (!temp.AudioHash.AsSpan().SequenceEqual(original.AudioHash) || (temp.Id3v1Bytes is null) != (original.Id3v1Bytes is null))
        {
            return false;
        }

        var tags = Mp3TagReader.Read(tempPath);
        return tags.CanWrite && Enum.GetValues<TagField>().All(field =>
            tags.Values[field] == (changes.TryGetValue(field, out var changed)
                ? (string.IsNullOrEmpty(changed) ? null : changed)
                : originalValues[field]));
    }

    private void Discard(RecoveryRecord record)
    {
        DeleteIfExists(record.TempPath);
        recoveryStore.Delete(record.Id);
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
