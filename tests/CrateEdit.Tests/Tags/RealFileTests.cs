using CrateEdit.Core.Tags;
using CrateEdit.Core.Tags.Writing;

namespace CrateEdit.Tests.Tags;

/// <summary>
/// Runs the preservation checks against real MP3 and WAV files when CRATEEDIT_SAMPLES points to a
/// folder of them. Skipped otherwise (real music cannot be committed). The files are copied to a
/// temporary folder first; the originals are only read. Safe-write tests copy to
/// CRATEEDIT_WRITE_DIR if set (e.g. a folder on a USB stick or a cloud-sync drive), so the
/// write technique can be checked on that storage.
/// </summary>
public class RealFileTests : Mp3TagTestBase
{
    private const string EditSuffix = " ✓";

    private static readonly string[] InfoFieldIds = ["INAM", "IART", "IPRD", "IGNR", "ICMT", "ICRD"];

    private static readonly string[] Id3FieldIds = ["TPE1", "TIT2", "TALB", "TCON", "TPUB", "TBPM", "TKEY", "COMM", "TYER", "TDRC"];

    private static string? SampleDirectory => Environment.GetEnvironmentVariable("CRATEEDIT_SAMPLES");

    private static string? WriteDirectory => Environment.GetEnvironmentVariable("CRATEEDIT_WRITE_DIR");

    public static TheoryData<string> Mp3Samples() => SampleNames("*.mp3", "MP3");

    public static TheoryData<string> WavSamples() => SampleNames("*.wav", "WAV");

    /// <summary>The sample file names, or one placeholder case that skips when there are none.</summary>
    private static TheoryData<string> SampleNames(string pattern, string format)
    {
        var files = SampleDirectory is { } dir && Directory.Exists(dir) ? Directory.GetFiles(dir, pattern) : [];
        return files.Length > 0
            ? new(files.Select(Path.GetFileName)!)
            : new($"(set CRATEEDIT_SAMPLES to a folder with {format} files to run)");
    }

    /// <summary>The full path of a sample; skips the test if there is no such sample.</summary>
    private static string SamplePath(string fileName)
    {
        var path = SampleDirectory is null ? null : Path.Combine(SampleDirectory, fileName);
        Assert.SkipWhen(path is null || !File.Exists(path), "No samples.");
        return path!;
    }

    /// <summary>The frame IDs of all fields, with the year frame of the file's ID3 version.</summary>
    private static string[] FieldFrameIds(ParsedMp3 file) =>
        ["TPE1", "TIT2", "TALB", "TCON", "TPUB", "TBPM", "TKEY", "COMM", file.Version == 4 ? "TDRC" : "TYER"];

    /// <summary>The bytes of an MP3 sample; fails the test if CrateEdit cannot write its tag.</summary>
    private byte[] WritableMp3Sample(string fileName)
    {
        var source = File.ReadAllBytes(SamplePath(fileName));
        var tags = Read(source);
        Assert.True(tags.CanWrite, tags.WriteProblem?.ToString());
        return source;
    }

    private static void CopySample(string fileName, string destination) => File.Copy(SamplePath(fileName), destination);

    [Theory]
    [MemberData(nameof(Mp3Samples))]
    [Trait("Req", "REQ-088")]
    [Trait("Req", "REQ-130")]
    public void Editing_all_fields_of_a_real_mp3_changes_only_those_frames(string fileName)
    {
        // Arrange
        var source = WritableMp3Sample(fileName);

        // Act
        var edited = Edit(source, EditEveryField(EditSuffix));

        // Assert
        var before = Mp3Fixture.Parse(source);
        AssertOnlyEdited(before, Mp3Fixture.Parse(edited), FieldFrameIds(before));
    }

    [Theory]
    [MemberData(nameof(Mp3Samples))]
    [Trait("Req", "REQ-006")]
    [Trait("Req", "REQ-088")]
    public void Editing_the_comment_of_a_real_mp3_keeps_comments_with_a_description(string fileName)
    {
        // Comments with a description, e.g. a store's "ID3v1 Comment", are other software's data (ADR-0012).

        // Arrange
        var source = WritableMp3Sample(fileName);

        // Act
        var edited = Edit(source, TagField.Comment, "Edited comment" + EditSuffix);

        // Assert
        var before = Mp3Fixture.Parse(source);
        var after = Mp3Fixture.Parse(edited);
        AssertSameFrames(before.CommentsWithDescription, after.CommentsWithDescription);
        Assert.Single(after.CommentsWithoutDescription);
    }

    [Theory]
    [MemberData(nameof(Mp3Samples))]
    [Trait("Req", "REQ-130")]
    public void Edited_values_of_a_real_mp3_are_read_back(string fileName)
    {
        // Arrange
        var source = WritableMp3Sample(fileName);
        var changes = EditEveryField(EditSuffix);

        // Act
        var edited = Edit(source, changes);

        // Assert
        AssertValues(edited, changes);
    }

    [Theory]
    [MemberData(nameof(Mp3Samples))]
    [Trait("Req", "REQ-088")]
    public void Safe_write_of_a_real_mp3_succeeds_and_cleans_up(string fileName)
    {
        // Arrange
        using var folder = new TemporaryFolder(WriteDirectory);
        using var recovery = new TemporaryFolder();
        var path = folder.Combine(fileName);
        CopySample(fileName, path);
        var store = new FileRecoveryStore(recovery.Path);
        var changes = new Dictionary<TagField, string?> { [TagField.Label] = "Edited Label" + EditSuffix };

        // Act
        var result = new SafeFileWriter(store).Write(path, changes);

        // Assert
        Assert.True(result.IsSuccess, result.ToString());
        Assert.Equal("Edited Label" + EditSuffix, Mp3TagReader.Read(path).Values[TagField.Label]);
        Assert.Empty(store.LoadAll());
    }

    [Theory]
    [MemberData(nameof(Mp3Samples))]
    [Trait("Req", "REQ-092")]
    public void Crash_during_a_non_atomic_replace_of_a_real_mp3_is_finished_by_recovery(string fileName)
    {
        // Arrange: a crash during the replace left the file missing (possible on FAT32 or cloud drives).
        using var folder = new TemporaryFolder(WriteDirectory);
        using var recovery = new TemporaryFolder();
        var path = folder.Combine(fileName);
        CopySample(fileName, path);
        var store = new FileRecoveryStore(recovery.Path);
        var crashing = new SafeFileWriter(store)
        {
            AfterStep = step => { if (step == WriteStep.TempVerified) throw new OperationCanceledException(); },
        };
        Assert.Throws<OperationCanceledException>(() => crashing.Write(path, new Dictionary<TagField, string?> { [TagField.Key] = "8A" }));
        File.Delete(path);

        // Act
        var outcome = Assert.Single(RecoveryReconciler.ReconcileAll(store)).Outcome;

        // Assert
        Assert.Equal(RecoveryOutcome.WriteCompleted, outcome);
        Assert.Equal("8A", Mp3TagReader.Read(path).Values[TagField.Key]);
        Assert.False(File.Exists(SafeFileWriter.TempPathFor(path)));
    }

    [Theory]
    [MemberData(nameof(WavSamples))]
    [Trait("Req", "REQ-003")]
    [Trait("Req", "REQ-088")]
    public void Safe_write_of_all_fields_of_a_real_wav_keeps_its_audio_and_other_data(string fileName)
    {
        // Inside the tag chunks, everything that is not one of the edited fields must survive,
        // e.g. Traktor's "NITR" INFO entry and Serato's GEOB frames.

        // Arrange
        using var folder = new TemporaryFolder();
        var path = folder.Combine(fileName);
        CopySample(fileName, path);
        var before = WavFixture.Parse(File.ReadAllBytes(path));
        var writer = new SafeFileWriter(new FileRecoveryStore(folder.Combine("recovery")));

        // Act
        var result = writer.Write(path, EditEveryField(EditSuffix));

        // Assert
        Assert.True(result.IsSuccess, result.ToString());
        var after = WavFixture.Parse(File.ReadAllBytes(path));
        Assert.Equal(before.Chunk("data").Offset, after.Chunk("data").Offset);
        WavAssert.OtherChunksKept(before, after);
        WavAssert.OtherInfoEntriesKept(before, after, InfoFieldIds);
        WavAssert.OtherId3FramesKept(before, after, Id3FieldIds);
    }

    [Theory]
    [MemberData(nameof(WavSamples))]
    [Trait("Req", "REQ-003")]
    public void Edited_values_of_a_real_wav_are_read_back(string fileName)
    {
        // Arrange
        using var folder = new TemporaryFolder();
        var path = folder.Combine(fileName);
        CopySample(fileName, path);
        var writer = new SafeFileWriter(new FileRecoveryStore(folder.Combine("recovery")));
        var changes = EditEveryField(EditSuffix);

        // Act
        writer.Write(path, changes);

        // Assert
        AssertValues(TagReader.Read(path), changes);
    }
}
