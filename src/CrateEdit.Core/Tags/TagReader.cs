namespace CrateEdit.Core.Tags;

/// <summary>Reads the tags of any supported audio file (<see href="https://github.com/thomaswening/CrateEdit/blob/main/docs/requirements.md#req-003">REQ-003</see>). Never modifies the file.</summary>
public static class TagReader
{
    /// <summary>True if the file's format is supported, judged by its extension.</summary>
    public static bool IsSupported(string path) => AudioFormats.Detect(path) is not null;

    /// <summary>
    /// Reads the file's tags. An unsupported format is reported as
    /// <see cref="TagProblem.UnsupportedFormat"/> with no values.
    /// </summary>
    public static FileTags Read(string path) => AudioFormats.Detect(path) switch
    {
        AudioFormat.Mp3 => Mp3TagReader.Read(path),
        AudioFormat.Wav => WavTagReader.Read(path),
        _ => new FileTags(TagValues.Empty, new TagNotWritable(TagProblem.UnsupportedFormat)),
    };
}

/// <summary>The audio file formats CrateEdit reads and writes.</summary>
public enum AudioFormat
{
    Mp3,
    Wav,
}

/// <summary>Determines a file's format.</summary>
public static class AudioFormats
{
    /// <summary>The format of <paramref name="path"/> by its extension, or null if unsupported.</summary>
    public static AudioFormat? Detect(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".mp3" => AudioFormat.Mp3,
        ".wav" or ".wave" => AudioFormat.Wav,
        _ => null,
    };
}
