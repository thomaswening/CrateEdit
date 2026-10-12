using System.Text.Json;
using Serilog;

namespace CrateEdit.Core.Tags.Writing;

/// <summary>
/// Keeps each recovery record as a JSON file in the app data folder. Records are written to a
/// temporary file, flushed and then renamed, so a crash leaves either the old or the new record.
/// Will move into the SQLite store in Phase 2 (<see href="https://github.com/thomaswening/CrateEdit/blob/main/docs/adr/0004-sqlite-local-state.md">ADR-0004</see>).
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

        var records = new List<RecoveryRecord>();
        foreach (var file in Directory.EnumerateFiles(directory, "*" + Extension))
        {
            try
            {
                records.Add(JsonSerializer.Deserialize<RecoveryRecord>(File.ReadAllBytes(file))
                    ?? throw new JsonException("Empty record."));
            }
            catch (JsonException exception)
            {
                // Never delete what cannot be read: it is left in place for diagnosis, and the
                // remaining records are still reconciled.
                Log.ForContext<FileRecoveryStore>().Error(exception, "Unreadable recovery record {File}", Path.GetFileName(file));
            }
        }

        return records;
    }

    private string PathFor(Guid id) => Path.Combine(directory, id.ToString("N") + Extension);
}
