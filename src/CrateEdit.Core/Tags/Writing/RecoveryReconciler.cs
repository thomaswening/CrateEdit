using System.Security.Cryptography;

namespace CrateEdit.Core.Tags.Writing;

/// <summary>How an interrupted write was resolved.</summary>
public enum RecoveryOutcome
{
    /// <summary>The original was still intact; the unfinished write was discarded.</summary>
    OriginalKept,

    /// <summary>The verified new content was already in place, or the interrupted rename was finished.</summary>
    WriteCompleted,

    /// <summary>The original was rebuilt from the recovery material.</summary>
    OriginalRestored,

    /// <summary>
    /// Neither the original nor the new content could be confirmed, e.g. because another program
    /// changed the file. Nothing was touched; the record is kept for the user to resolve (REQ-093).
    /// </summary>
    Unresolved,
}

/// <summary>
/// Finishes or undoes writes that were interrupted, e.g. by a crash or a power loss (ADR-0010,
/// REQ-092). Never leaves a file worse than it found it: when the situation is unclear, it does
/// nothing and reports <see cref="RecoveryOutcome.Unresolved"/>.
/// </summary>
public static class RecoveryReconciler
{
    /// <summary>Resolves every recorded write that did not finish, e.g. at startup.</summary>
    public static IReadOnlyList<(RecoveryRecord Record, RecoveryOutcome Outcome)> ReconcileAll(IRecoveryStore store) =>
        store.LoadAll().Select(record => (record, Reconcile(record, store))).ToList();

    public static RecoveryOutcome Reconcile(RecoveryRecord record, IRecoveryStore store)
    {
        var outcome = Decide(record);
        if (outcome != RecoveryOutcome.Unresolved)
        {
            DeleteIfExists(record.TempPath);
            store.Delete(record.Id);
        }

        return outcome;
    }

    private static RecoveryOutcome Decide(RecoveryRecord record)
    {
        var target = record.TargetPath;
        var temp = record.TempPath;

        if (File.Exists(target))
        {
            var targetHash = Mp3Layout.HashFile(target);
            if (record.NewFileHash is { } newHash && targetHash.AsSpan().SequenceEqual(newHash))
            {
                return RecoveryOutcome.WriteCompleted;
            }

            if (targetHash.AsSpan().SequenceEqual(record.OriginalFileHash))
            {
                return RecoveryOutcome.OriginalKept;
            }

            // The target is neither version. If it still carries the original audio, the original
            // can be rebuilt from it; otherwise something else changed the file, so leave it alone.
            return TryRestoreFrom(target, record) ? RecoveryOutcome.OriginalRestored : RecoveryOutcome.Unresolved;
        }

        // The target is missing: the replace was interrupted on a file system where it is not
        // atomic. A verified temporary file can simply be moved into place.
        if (File.Exists(temp) && record.NewFileHash is { } verified && Mp3Layout.HashFile(temp).AsSpan().SequenceEqual(verified))
        {
            File.Move(temp, target);
            return RecoveryOutcome.WriteCompleted;
        }

        return File.Exists(temp) && TryRestoreFrom(temp, record) ? RecoveryOutcome.OriginalRestored : RecoveryOutcome.Unresolved;
    }

    /// <summary>
    /// Rebuilds the original from <paramref name="source"/>'s audio and the recorded original tags,
    /// if the audio is the original's. Writes to a separate file, checks it is byte-identical to the
    /// original, then puts it in place.
    /// </summary>
    private static bool TryRestoreFrom(string source, RecoveryRecord record)
    {
        Mp3Layout layout;
        try
        {
            layout = Mp3Layout.Read(source);
        }
        catch (EndOfStreamException)
        {
            return false;
        }

        if (!layout.AudioHash.AsSpan().SequenceEqual(record.OriginalAudioHash))
        {
            return false;
        }

        var rebuilt = record.TargetPath + ".crateedit-restore";
        using (var output = new FileStream(rebuilt, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            output.Write(record.OriginalId3v2Tag);
            input.Position = layout.AudioStart;
            CopyBytes(input, output, layout.AudioLength);
            if (record.OriginalId3v1Tag is { } id3v1)
            {
                output.Write(id3v1);
            }

            output.Flush(flushToDisk: true);
        }

        if (!Mp3Layout.HashFile(rebuilt).AsSpan().SequenceEqual(record.OriginalFileHash))
        {
            File.Delete(rebuilt);
            return false;
        }

        if (File.Exists(record.TargetPath))
        {
            File.Replace(rebuilt, record.TargetPath, destinationBackupFileName: null);
        }
        else
        {
            File.Move(rebuilt, record.TargetPath);
        }

        return true;
    }

    private static void CopyBytes(Stream source, Stream destination, long count)
    {
        var buffer = new byte[81920];
        while (count > 0)
        {
            var read = source.Read(buffer, 0, (int)Math.Min(buffer.Length, count));
            if (read == 0)
            {
                throw new EndOfStreamException();
            }

            destination.Write(buffer, 0, read);
            count -= read;
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
