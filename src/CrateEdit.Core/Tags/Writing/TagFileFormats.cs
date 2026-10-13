using CrateEdit.Core.Results;
using CrateEdit.Core.Tags.Id3;
using CrateEdit.Core.Tags.Riff;

namespace CrateEdit.Core.Tags.Writing;

/// <summary>What the safe write needs from a file format (<see href="https://github.com/thomaswening/CrateEdit/blob/main/docs/adr/0010-safe-write-technique.md">ADR-0010</see>).</summary>
internal interface ITagFileFormat
{
    FileTags Read(string path);

    /// <summary>Writes the source with the changes applied to a new destination file; never modifies the source.</summary>
    Result Rewrite(string sourcePath, string destinationPath, IReadOnlyDictionary<TagField, string?> changes);

    /// <summary>Where the audio is, so it can be verified unchanged and the original rebuilt around it.</summary>
    AudioLayout ReadLayout(string path);

    /// <summary>Format-specific structural checks of a rewritten file against its original, beyond the audio.</summary>
    bool IsConsistentRewrite(AudioLayout original, AudioLayout rewritten);
}

internal static class TagFileFormats
{
    /// <summary>The format implementation for <paramref name="path"/>, judged by its extension, or null if unsupported.</summary>
    public static ITagFileFormat? For(string path) => AudioFormats.Detect(path) switch
    {
        AudioFormat.Mp3 => Mp3FileFormat.Instance,
        AudioFormat.Wav => WavFileFormat.Instance,
        _ => null,
    };
}

/// <summary>MP3: <c>[ID3v2 tag][audio][ID3v1 tag?]</c>.</summary>
internal sealed class Mp3FileFormat : ITagFileFormat
{
    public static Mp3FileFormat Instance { get; } = new();

    public FileTags Read(string path) => Mp3TagReader.Read(path);

    public Result Rewrite(string sourcePath, string destinationPath, IReadOnlyDictionary<TagField, string?> changes) =>
        Mp3TagWriter.Rewrite(sourcePath, destinationPath, changes);

    /// <summary>An ID3v1 tag is updated but never added or removed, so the suffix keeps its length.</summary>
    public bool IsConsistentRewrite(AudioLayout original, AudioLayout rewritten) =>
        original.Suffix.Length == rewritten.Suffix.Length;

    public AudioLayout ReadLayout(string path)
    {
        long prefixLength;
        long suffixLength = 0;
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            prefixLength = Mp3TagReader.ReadId3v2(stream)?.Header.TotalSizeBytes ?? 0;
            if (stream.Length - prefixLength >= Id3v1Tag.SizeBytes)
            {
                var last = new byte[Id3v1Tag.SizeBytes];
                stream.Position = stream.Length - Id3v1Tag.SizeBytes;
                stream.ReadExactly(last);
                suffixLength = Id3v1Tag.IsPresent(last) ? Id3v1Tag.SizeBytes : 0;
            }
        }

        return AudioLayout.Read(path, prefixLength, suffixLength);
    }
}

/// <summary>WAV: <c>[RIFF header and chunks before the audio][audio][chunks after it]</c>.</summary>
internal sealed class WavFileFormat : ITagFileFormat
{
    public static WavFileFormat Instance { get; } = new();

    public FileTags Read(string path) => WavTagReader.Read(path);

    public Result Rewrite(string sourcePath, string destinationPath, IReadOnlyDictionary<TagField, string?> changes) =>
        WavTagWriter.Rewrite(sourcePath, destinationPath, changes);

    /// <summary>Chunks may be added (INFO, id3) and resized, so only the audio is compared.</summary>
    public bool IsConsistentRewrite(AudioLayout original, AudioLayout rewritten) => true;

    public AudioLayout ReadLayout(string path)
    {
        long prefixLength;
        long suffixLength;
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var riff = RiffFile.Read(stream);
            if (riff is null || riff.Problem is not null)
            {
                throw new EndOfStreamException("Not a readable WAV file.");
            }

            var data = riff.Data;
            prefixLength = data.BodyOffset;
            suffixLength = riff.Length - data.BodyOffset - data.Size;
        }

        return AudioLayout.Read(path, prefixLength, suffixLength);
    }
}
