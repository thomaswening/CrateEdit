using CrateEdit.Core.Tags;
using CrateEdit.Core.Tags.Writing;
using CrateEdit.Tests.Tags;

namespace CrateEdit.Tests.Writing;

/// <summary>
/// Tests that the recovery material of a safe write stays small (<see href="https://github.com/thomaswening/CrateEdit/blob/main/docs/requirements.md#req-091">REQ-091</see>, <see href="https://github.com/thomaswening/CrateEdit/blob/main/docs/adr/0010-safe-write-technique.md">ADR-0010</see>):
/// it holds the original tag and hashes, never a copy of the audio, so a long mix needs no more
/// space in app data than a short track with the same tag.
/// </summary>
public sealed class RecoveryMaterialSizeTests : IDisposable
{
    private sealed class SimulatedCrash : Exception;

    private static readonly Dictionary<TagField, string?> Changes = new()
    {
        [TagField.Artist] = "Röyksopp",
        [TagField.Comment] = "A comment that does not fit into the padding of the original tag, so the tag must grow",
    };

    /// <summary>
    /// Room for everything in a record besides the tag: paths, lengths and three SHA-256 hashes,
    /// about 500 bytes as JSON.
    /// </summary>
    private const int RecordOverhead = 4 * 1024;

    /// <summary>The record stores the tag as Base64, which is a third larger than the tag; 2 leaves margin.</summary>
    private const int TagEncodingFactor = 2;

    private readonly string _dir = Directory.CreateTempSubdirectory("crateedit-recovery-size-").FullName;
    private readonly string _path;
    private readonly string _storeDirectory;

    public RecoveryMaterialSizeTests()
    {
        _path = Path.Combine(_dir, "track.mp3");
        _storeDirectory = Path.Combine(_dir, "recovery");
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    /// <summary>Starts a write that crashes right after <paramref name="step"/>, so its recovery material stays in the store.</summary>
    private void CrashAfter(WriteStep step)
    {
        var writer = new SafeFileWriter(new FileRecoveryStore(_storeDirectory))
        {
            AfterStep = current => { if (current == step) throw new SimulatedCrash(); },
        };
        Assert.Throws<SimulatedCrash>(() => writer.Write(_path, Changes));
    }

    /// <summary>Total size of all files in the recovery store, in bytes.</summary>
    private long RecoveryMaterialBytes() =>
        Directory.EnumerateFiles(_storeDirectory).Sum(file => new FileInfo(file).Length);

    [Theory]
    [Trait("Req", "REQ-091")]
    [InlineData(20_000)]
    [InlineData(20_000_000)]
    public void Recovery_material_is_bounded_by_the_tag_whatever_the_audio_length(int audioLength)
    {
        // Arrange
        var tag = Mp3Fixture.Tag(4, [.. Frames.BeatportLike(), .. Frames.DjSoftwareData()], padding: 16);
        var audio = Mp3Fixture.Audio(audioLength);
        File.WriteAllBytes(_path, [.. tag, .. audio]);
        var limit = (TagEncodingFactor * tag.Length) + RecordOverhead;

        // Act: after the temporary file is verified, the record is complete and at its largest.
        CrashAfter(WriteStep.TempVerified);

        // Assert
        Assert.InRange(RecoveryMaterialBytes(), 1, limit);
    }
}
