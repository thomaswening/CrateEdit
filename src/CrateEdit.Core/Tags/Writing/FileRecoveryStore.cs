using System.Text.Json;

namespace CrateEdit.Core.Tags.Writing;

/// <summary>
/// Keeps each recovery record as a JSON file in the app data folder. Records are written to a
/// temporary file, flushed and then renamed, so a crash leaves either the old or the new record.
/// Will move into the SQLite store in Phase 2 (ADR-0004).
/// </summary>
public sealed class FileRecoveryStore(string directory) : IRecoveryStore
{
    private const string Extension = ".json";
    private const string PartialExtension = ".json.partial";

    public void Save(RecoveryRecord record)
    {
        Directory.CreateDirectory(directory);
        var path = PathFor(record.Id);
        var partial = path[..^Extension.Length] + PartialExtension;

        using (var stream = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, record);
            stream.Flush(flushToDisk: true);
        }

        File.Move(partial, path, overwrite: true);
    }

    public void Delete(Guid id) => File.Delete(PathFor(id));

    public IReadOnlyList<RecoveryRecord> LoadAll()
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        // A partial file means the crash happened while saving it; the previous version (if any)
        // is still the valid one.
        foreach (var partial in Directory.EnumerateFiles(directory, "*" + PartialExtension))
        {
            File.Delete(partial);
        }

        return Directory.EnumerateFiles(directory, "*" + Extension)
            .Select(file => JsonSerializer.Deserialize<RecoveryRecord>(File.ReadAllBytes(file))!)
            .ToList();
    }

    private string PathFor(Guid id) => Path.Combine(directory, id.ToString("N") + Extension);
}
