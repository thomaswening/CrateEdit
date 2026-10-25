using System.Text;
using CrateEdit.Core.Tags;
using CrateEdit.Core.Tags.Writing;

namespace CrateEdit.Tests.Tags;

/// <summary>WAV reading and writing (<see href="https://github.com/thomaswening/CrateEdit/blob/main/docs/adr/0011-wav-tagging.md">ADR-0011</see>): INFO for Rekordbox, "id3 " for everything, all else untouched.</summary>
public sealed class WavTagTests : IDisposable
{
    /// <summary>"RIFF", the size and "WAVE": the part of the file before the first chunk.</summary>
    private const int RiffHeaderLength = 12;

    private readonly string _dir = Directory.CreateTempSubdirectory("crateedit-wav-").FullName;
    private int _counter;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string WriteFile(byte[] content)
    {
        var path = Path.Combine(_dir, $"source-{_counter++}.wav");
        File.WriteAllBytes(path, content);
        return path;
    }

    private FileTags Read(byte[] file) => WavTagReader.Read(WriteFile(file));

    private string? ReadValue(byte[] file, TagField field) => Read(file).Values[field];

    /// <summary>Applies the changes and returns the edited file; fails the test if the writer refuses or changes the source.</summary>
    private byte[] Edit(byte[] source, Dictionary<TagField, string?> changes)
    {
        var sourcePath = WriteFile(source);
        var destination = Path.Combine(_dir, $"edited-{_counter++}.wav");
        var result = WavTagWriter.Rewrite(sourcePath, destination, changes);
        Assert.True(result.IsSuccess, result.ToString());
        Assert.Equal(source, File.ReadAllBytes(sourcePath));
        return File.ReadAllBytes(destination);
    }

    private byte[] Edit(byte[] source, TagField field, string? value) => Edit(source, new Dictionary<TagField, string?> { [field] = value });

    /// <summary>Like the owner's WAV: fmt, data, then INFO and id3 after the audio, plus chunks DJ and audio software add.</summary>
    private static byte[] FullFile() => WavFixture.Build(
        WavFixture.Fmt(),
        WavFixture.Chunk("bext", Frames.RandomBytes(602, 21)),
        WavFixture.Data(),
        WavFixture.Chunk("cue ", Frames.RandomBytes(28, 22)),
        WavFixture.Chunk("XYZW", Frames.RandomBytes(5, 23)),
        WavFixture.Info(("INAM", "Info Title"), ("IART", "Info Artist"), ("ISFT", "Some Encoder 1.0")),
        WavFixture.Id3(
        [
            Frames.Text("TIT2", "Id3 Title", TextEncodings.Latin1),
            Frames.Text("TALB", "Id3 Album", TextEncodings.Latin1),
            Frames.Text("TPUB", "Strictly Rhythm", TextEncodings.Latin1),
            Frames.Text("TBPM", "122", TextEncodings.Latin1),
            .. Frames.DjSoftwareData(),
        ]));

    private static byte[] AudioOnly() => WavFixture.Build(WavFixture.Fmt(), WavFixture.Data());

    private static readonly Dictionary<TagField, string?> CrashTestChanges = new()
    {
        [TagField.Artist] = "Röyksopp",
        [TagField.Label] = "Label",
    };

    private static void CrashWhileWriting(string path, FileRecoveryStore store, WriteStep crashAfter)
    {
        var writer = new SafeFileWriter(store) { AfterStep = step => { if (step == crashAfter) throw new OperationCanceledException(); } };
        Assert.Throws<OperationCanceledException>(() => writer.Write(path, CrashTestChanges));
    }

    [Fact]
    [Trait("Req", "REQ-006")]
    public void Info_is_read_first_and_id3_for_what_info_cannot_hold()
    {
        // Arrange
        var source = FullFile();

        // Act
        var values = Read(source).Values;

        // Assert
        Assert.Equal("Info Title", values[TagField.Title]);
        Assert.Equal("Info Artist", values[TagField.Artist]);
        Assert.Equal("Id3 Album", values[TagField.Album]);
        Assert.Equal("Strictly Rhythm", values[TagField.Label]);
        Assert.Equal("122", values[TagField.Bpm]);
    }

    [Fact]
    [Trait("Req", "REQ-128")]
    public void Info_text_is_read_as_utf8()
    {
        // Arrange
        var source = WavFixture.Build(WavFixture.Fmt(), WavFixture.Data(), WavFixture.Info(("INAM", "Röyksopp")));

        // Act
        var title = ReadValue(source, TagField.Title);

        // Assert
        Assert.Equal("Röyksopp", title);
    }

    [Fact]
    [Trait("Req", "REQ-128")]
    public void Info_text_that_is_not_utf8_is_read_as_windows_1252()
    {
        // Arrange
        byte[] latin1Title = [.. Encoding.Latin1.GetBytes("Röyksopp"), 0];
        var source = WavFixture.Build(WavFixture.Fmt(), WavFixture.Data(), WavFixture.InfoRaw(("INAM", latin1Title)));

        // Act
        var title = ReadValue(source, TagField.Title);

        // Assert
        Assert.Equal("Röyksopp", title);
    }

    [Fact]
    [Trait("Req", "REQ-088")]
    [Trait("Req", "REQ-130")]
    public void Editing_a_field_leaves_all_other_chunks_byte_identical()
    {
        // Arrange
        var source = FullFile();

        // Act
        var edited = Edit(source, TagField.Title, "Ärzte");

        // Assert
        WavAssert.OnlyTagChunksChanged(WavFixture.Parse(source), WavFixture.Parse(edited));
    }

    [Fact]
    [Trait("Req", "REQ-130")]
    public void Editing_a_field_writes_it_to_both_tag_chunks()
    {
        // Arrange
        var source = FullFile();

        // Act
        var edited = Edit(source, TagField.Title, "Ärzte");

        // Assert
        var after = WavFixture.Parse(edited);
        Assert.Equal("Ärzte", after.InfoText("INAM"));
        Assert.Equal("Ärzte", after.Id3Frame("TIT2").Text);
    }

    [Fact]
    [Trait("Req", "REQ-088")]
    [Trait("Req", "REQ-130")]
    public void Editing_a_field_keeps_the_other_info_entries_and_id3_frames()
    {
        // Arrange
        var source = FullFile();

        // Act
        var edited = Edit(source, TagField.Title, "Ärzte");

        // Assert
        var before = WavFixture.Parse(source);
        var after = WavFixture.Parse(edited);
        WavAssert.OtherInfoEntriesKept(before, after, "INAM");
        WavAssert.OtherId3FramesKept(before, after, "TIT2");
    }

    [Fact]
    [Trait("Req", "REQ-130")]
    public void Editing_a_field_info_cannot_hold_leaves_the_info_chunk_byte_identical()
    {
        // Arrange: trailing zero padding inside the INFO list would be lost if the chunk were rebuilt.
        var info = WavFixture.InfoWithTrailingZeros(4, ("INAM", "Title"), ("ISFT", "Encoder"));
        var source = WavFixture.Build(WavFixture.Fmt(), WavFixture.Data(), info,
            WavFixture.Id3([Frames.Text("TPUB", "Old Label", TextEncodings.Latin1)]));

        // Act
        var edited = Edit(source, TagField.Label, "New Label");

        // Assert
        var infoBefore = WavFixture.Parse(source).Chunk("LIST");
        var infoAfter = WavFixture.Parse(edited).Chunk("LIST");
        Assert.True(infoBefore.SameBytesAs(infoAfter));
    }

    [Fact]
    [Trait("Req", "REQ-006")]
    public void Missing_info_is_created_after_the_audio()
    {
        // Arrange
        var source = WavFixture.Build(WavFixture.Fmt(), WavFixture.Data(), WavFixture.Id3([Frames.Text("TIT2", "Title", TextEncodings.Latin1)]));

        // Act
        var edited = Edit(source, TagField.Year, "1977");

        // Assert
        var before = WavFixture.Parse(source);
        var after = WavFixture.Parse(edited);
        Assert.Equal(before.Chunk("data").Offset, after.Chunk("data").Offset);
        Assert.Equal("LIST", after.LastChunk.Id);
    }

    [Fact]
    [Trait("Req", "REQ-006")]
    public void Missing_info_is_created_with_all_current_values_it_can_hold()
    {
        // Arrange
        var source = WavFixture.Build(WavFixture.Fmt(), WavFixture.Data(), WavFixture.Id3(
        [
            Frames.Text("TIT2", "Title", TextEncodings.Latin1),
            Frames.Text("TPE1", "Artist", TextEncodings.Latin1),
            Frames.Text("TPUB", "Label", TextEncodings.Latin1),
        ]));

        // Act
        var edited = Edit(source, TagField.Year, "1977");

        // Assert
        var after = WavFixture.Parse(edited);
        Assert.Equal(["IART", "ICRD", "INAM"], after.InfoEntries.Keys.Order());
        Assert.Equal("Title", after.InfoText("INAM"));
        Assert.Equal("Artist", after.InfoText("IART"));
        Assert.Equal("1977", after.InfoText("ICRD"));
    }

    [Fact]
    [Trait("Req", "REQ-006")]
    public void Missing_id3_is_created_with_all_current_values()
    {
        // Arrange
        var source = WavFixture.Build(WavFixture.Fmt(), WavFixture.Data(), WavFixture.Info(("INAM", "Title"), ("IART", "Artist")));

        // Act
        var edited = Edit(source, TagField.Key, "8A");

        // Assert
        Assert.Equal(["TIT2", "TKEY", "TPE1"], WavFixture.Parse(edited).Id3FrameIds.Order());
    }

    [Fact]
    [Trait("Req", "REQ-130")]
    public void Creating_the_id3_chunk_leaves_the_info_chunk_byte_identical()
    {
        // Arrange
        var source = WavFixture.Build(WavFixture.Fmt(), WavFixture.Data(), WavFixture.Info(("INAM", "Title"), ("IART", "Artist")));

        // Act
        var edited = Edit(source, TagField.Key, "8A");

        // Assert
        var infoBefore = WavFixture.Parse(source).Chunk("LIST");
        var infoAfter = WavFixture.Parse(edited).Chunk("LIST");
        Assert.True(infoBefore.SameBytesAs(infoAfter));
    }

    [Fact]
    [Trait("Req", "REQ-130")]
    public void Clearing_a_field_removes_it_from_both_chunks()
    {
        // Arrange
        var source = FullFile();

        // Act
        var edited = Edit(source, TagField.Title, null);

        // Assert
        var after = WavFixture.Parse(edited);
        Assert.Null(after.InfoText("INAM"));
        Assert.DoesNotContain("TIT2", after.Id3FrameIds);
        Assert.Null(ReadValue(edited, TagField.Title));
    }

    [Fact]
    [Trait("Req", "REQ-088")]
    public void Odd_sized_last_chunk_without_pad_byte_is_kept()
    {
        // Arrange
        var source = WavFixture.WithoutFinalPadByte(
            WavFixture.Build(WavFixture.Fmt(), WavFixture.Data(), WavFixture.Chunk("XYZW", [1, 2, 3])));
        var originalChunks = source[RiffHeaderLength..];

        // Act
        var edited = Edit(source, TagField.Title, "Title");

        // Assert
        Assert.Equal(originalChunks, edited[RiffHeaderLength..source.Length]);
    }

    [Fact]
    [Trait("Req", "REQ-088")]
    public void Chunks_added_after_an_odd_sized_last_chunk_without_pad_byte_are_readable()
    {
        // Arrange
        var source = WavFixture.WithoutFinalPadByte(
            WavFixture.Build(WavFixture.Fmt(), WavFixture.Data(), WavFixture.Chunk("XYZW", [1, 2, 3])));

        // Act
        var edited = Edit(source, TagField.Title, "Title");

        // Assert
        Assert.Equal("Title", ReadValue(edited, TagField.Title));
    }

    public static TheoryData<string, byte[], TagProblem> UnwritableFiles() => new()
    {
        { "RF64 (over 4 GB)", [.. "RF64"u8, .. AudioOnly()[4..]], TagProblem.UnsupportedVersion },
        { "truncated", AudioOnly()[..^100], TagProblem.Malformed },
        { "two data chunks", WavFixture.Build(WavFixture.Fmt(), WavFixture.Data(), WavFixture.Data(100)), TagProblem.Malformed },
        { "two id3 chunks", WavFixture.Build(WavFixture.Fmt(), WavFixture.Data(), WavFixture.Id3([]), WavFixture.Id3([])), TagProblem.Malformed },
        { "trailing bytes", [.. AudioOnly(), 1, 2, 3], TagProblem.Malformed },
    };

    [Theory]
    [MemberData(nameof(UnwritableFiles))]
    [Trait("Req", "REQ-130")]
    public void Files_that_cannot_be_fully_parsed_are_reported_as_not_writable(string _, byte[] source, TagProblem expected)
    {
        // Act
        var tags = Read(source);

        // Assert
        Assert.Equal(expected, tags.WriteProblem?.Problem);
    }

    [Theory]
    [MemberData(nameof(UnwritableFiles))]
    [Trait("Req", "REQ-130")]
    public void Files_that_cannot_be_fully_parsed_are_refused_and_left_untouched(string _, byte[] source, TagProblem expected)
    {
        // Arrange
        var sourcePath = WriteFile(source);
        var destination = Path.Combine(_dir, "edited.wav");
        var changes = new Dictionary<TagField, string?> { [TagField.Title] = "New" };

        // Act
        var result = WavTagWriter.Rewrite(sourcePath, destination, changes);

        // Assert
        Assert.Equal(expected, Assert.IsType<TagNotWritable>(result.Error).Problem);
        Assert.False(File.Exists(destination));
        Assert.Equal(source, File.ReadAllBytes(sourcePath));
    }

    [Fact]
    [Trait("Req", "REQ-130")]
    public void Duplicated_info_and_id3_chunks_are_both_named()
    {
        // Arrange
        var source = WavFixture.Build(WavFixture.Fmt(), WavFixture.Data(),
            WavFixture.Info(("INAM", "One")), WavFixture.Info(("INAM", "Two")),
            WavFixture.Id3([]), WavFixture.Id3([]));

        // Act
        var problem = Read(source).WriteProblem;

        // Assert
        Assert.Equal(TagProblem.Malformed, problem?.Problem);
        Assert.Equal("LIST/INFO, id3", problem?.FrameId);
    }

    [Fact]
    [Trait("Req", "REQ-003")]
    public void Safe_write_of_a_wav_succeeds()
    {
        // Arrange
        var path = WriteFile(FullFile());
        var writer = new SafeFileWriter(new FileRecoveryStore(Path.Combine(_dir, "recovery")));
        var changes = new Dictionary<TagField, string?> { [TagField.Genre] = "House" };

        // Act
        var result = writer.Write(path, changes);

        // Assert
        Assert.True(result.IsSuccess, result.ToString());
        Assert.Equal("House", TagReader.Read(path).Values[TagField.Genre]);
    }

    [Fact]
    [Trait("Req", "REQ-092")]
    public void Safe_write_of_a_wav_that_crashes_before_the_replace_keeps_the_original()
    {
        // Arrange
        var original = FullFile();
        var path = WriteFile(original);
        var store = new FileRecoveryStore(Path.Combine(_dir, "recovery"));
        CrashWhileWriting(path, store, WriteStep.TempWritten);

        // Act
        RecoveryReconciler.ReconcileAll(store);

        // Assert
        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.Empty(store.LoadAll());
    }

    [Fact]
    [Trait("Req", "REQ-092")]
    public void Safe_write_of_a_wav_that_crashes_after_the_replace_is_completed()
    {
        // Arrange
        var original = FullFile();
        var expected = Edit(original, CrashTestChanges);
        var path = WriteFile(original);
        var store = new FileRecoveryStore(Path.Combine(_dir, "recovery"));
        CrashWhileWriting(path, store, WriteStep.Replaced);

        // Act
        RecoveryReconciler.ReconcileAll(store);

        // Assert
        Assert.Equal(expected, File.ReadAllBytes(path));
        Assert.Empty(store.LoadAll());
    }
}
