using System.Text;
using CrateEdit.Core.Tags;
using CrateEdit.Core.Tags.Writing;

namespace CrateEdit.Tests.Tags;

/// <summary>WAV reading and writing (<see href="https://github.com/thomaswening/CrateEdit/blob/main/docs/adr/0011-wav-tagging.md">ADR-0011</see>): INFO for Rekordbox, "id3 " for everything, all else untouched.</summary>
public sealed class WavTagTests : IDisposable
{
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

    private static string Utf8(byte[] value) => Encoding.UTF8.GetString(value).TrimEnd('\0');

    private static RawChunk Single(List<RawChunk> chunks, string id) => Assert.Single(chunks, c => c.Id == id);

    /// <summary>Like the owner's WAV: fmt, data, then INFO and id3 after the audio, plus chunks DJ and audio software add.</summary>
    private static byte[] FullFile() => WavFixture.Build(
        WavFixture.Fmt(),
        WavFixture.Chunk("bext", Frames.RandomBytes(602, 21)),
        WavFixture.Data(),
        WavFixture.Chunk("cue ", Frames.RandomBytes(28, 22)),
        WavFixture.Chunk("XYZW", Frames.RandomBytes(5, 23)),
        WavFixture.Info(("INAM", "Info Title"), ("IART", "Info Artist"), ("ISFT", "Some Encoder 1.0")),
        WavFixture.Id3([Frames.Text("TIT2", "Id3 Title", encoding: 0), Frames.Text("TALB", "Id3 Album", encoding: 0),
            Frames.Text("TPUB", "Strictly Rhythm", encoding: 0), Frames.Text("TBPM", "122", encoding: 0), .. Frames.DjSoftwareData()]));

    [Fact]
    [Trait("Req", "REQ-006")]
    public void Info_is_read_first_and_id3_for_what_info_cannot_hold()
    {
        var values = Read(FullFile()).Values;

        Assert.Equal("Info Title", values[TagField.Title]);
        Assert.Equal("Info Artist", values[TagField.Artist]);
        Assert.Equal("Id3 Album", values[TagField.Album]);
        Assert.Equal("Strictly Rhythm", values[TagField.Label]);
        Assert.Equal("122", values[TagField.Bpm]);
    }

    [Fact]
    [Trait("Req", "REQ-128")]
    public void Info_text_is_read_as_utf8_with_a_windows_1252_fallback()
    {
        var utf8 = WavFixture.Build(WavFixture.Fmt(), WavFixture.Data(), WavFixture.Info(("INAM", "Röyksopp")));
        var legacy = WavFixture.Build(WavFixture.Fmt(), WavFixture.Data(), WavFixture.InfoRaw(("INAM", [.. Encoding.Latin1.GetBytes("Röyksopp"), 0])));

        Assert.Equal("Röyksopp", Read(utf8).Values[TagField.Title]);
        Assert.Equal("Röyksopp", Read(legacy).Values[TagField.Title]);
    }

    [Fact]
    [Trait("Req", "REQ-088")]
    [Trait("Req", "REQ-130")]
    public void Editing_a_field_updates_both_tag_chunks_and_nothing_else()
    {
        var source = FullFile();

        var edited = Edit(source, TagField.Title, "Ärzte");

        var before = WavFixture.Parse(source);
        var after = WavFixture.Parse(edited);
        Assert.Equal(before.Select(c => c.Id), after.Select(c => c.Id));
        foreach (var (b, a) in before.Zip(after).Where(p => p.First.Id is not ("LIST" or "id3 ")))
        {
            Assert.True(b.SameBytesAs(a), $"{b} changed.");
        }

        var info = WavFixture.InfoEntries(Single(after, "LIST").Body);
        Assert.Equal("Ärzte", Utf8(info["INAM"]));
        Assert.Equal(WavFixture.InfoEntries(Single(before, "LIST").Body)["ISFT"], info["ISFT"]);

        var framesBefore = WavFixture.Id3Frames(Single(before, "id3 ").Body);
        var framesAfter = WavFixture.Id3Frames(Single(after, "id3 ").Body);
        Assert.Equal(framesBefore.Where(f => f.Id != "TIT2").Select(f => f.Body), framesAfter.Where(f => f.Id != "TIT2").Select(f => f.Body));
        Assert.Equal("Ärzte", Read(edited).Values[TagField.Title]);
    }

    [Fact]
    [Trait("Req", "REQ-130")]
    public void Editing_a_field_info_cannot_hold_leaves_the_info_chunk_byte_identical()
    {
        // Trailing zero padding inside the INFO list would be lost if the chunk were rebuilt.
        byte[] info = [.. WavFixture.Info(("INAM", "Title"), ("ISFT", "Encoder"))[8..], 0, 0, 0, 0];
        var source = WavFixture.Build(WavFixture.Fmt(), WavFixture.Data(), WavFixture.Chunk("LIST", info),
            WavFixture.Id3([Frames.Text("TPUB", "Old Label", encoding: 0)]));

        var edited = Edit(source, TagField.Label, "New Label");

        Assert.True(Single(WavFixture.Parse(source), "LIST").SameBytesAs(Single(WavFixture.Parse(edited), "LIST")));
        Assert.Equal("New Label", Read(edited).Values[TagField.Label]);
    }

    [Fact]
    [Trait("Req", "REQ-006")]
    public void Missing_info_is_created_after_the_audio_with_all_current_values()
    {
        var source = WavFixture.Build(WavFixture.Fmt(), WavFixture.Data(),
            WavFixture.Id3([Frames.Text("TIT2", "Title", encoding: 0), Frames.Text("TPE1", "Artist", encoding: 0), Frames.Text("TPUB", "Label", encoding: 0)]));

        var edited = Edit(source, TagField.Year, "1977");

        var after = WavFixture.Parse(edited);
        Assert.Equal(Single(WavFixture.Parse(source), "data").Offset, Single(after, "data").Offset);
        Assert.Equal("LIST", after[^1].Id);
        var info = WavFixture.InfoEntries(after[^1].Body);
        Assert.Equal(["IART", "ICRD", "INAM"], info.Keys.Order());
        Assert.Equal(("Title", "Artist", "1977"), (Utf8(info["INAM"]), Utf8(info["IART"]), Utf8(info["ICRD"])));
    }

    [Fact]
    [Trait("Req", "REQ-006")]
    public void Missing_id3_is_created_with_all_current_values()
    {
        var source = WavFixture.Build(WavFixture.Fmt(), WavFixture.Data(), WavFixture.Info(("INAM", "Title"), ("IART", "Artist")));

        var edited = Edit(source, TagField.Key, "8A");

        var after = WavFixture.Parse(edited);
        Assert.True(Single(WavFixture.Parse(source), "LIST").SameBytesAs(Single(after, "LIST")));
        Assert.Equal(["TIT2", "TKEY", "TPE1"], WavFixture.Id3Frames(Single(after, "id3 ").Body).Select(f => f.Id).Order());
        Assert.Equal("8A", Read(edited).Values[TagField.Key]);
    }

    [Fact]
    [Trait("Req", "REQ-130")]
    public void Clearing_a_field_removes_it_from_both_chunks()
    {
        var edited = Edit(FullFile(), TagField.Title, null);

        var after = WavFixture.Parse(edited);
        Assert.False(WavFixture.InfoEntries(Single(after, "LIST").Body).ContainsKey("INAM"));
        Assert.DoesNotContain(WavFixture.Id3Frames(Single(after, "id3 ").Body), f => f.Id == "TIT2");
        Assert.Null(Read(edited).Values[TagField.Title]);
    }

    [Fact]
    [Trait("Req", "REQ-088")]
    public void Odd_sized_last_chunk_without_pad_byte_is_kept_and_new_chunks_stay_aligned()
    {
        // Some writers omit the pad byte after an odd-sized final chunk.
        var source = WavFixture.Build(WavFixture.Fmt(), WavFixture.Data(), WavFixture.Chunk("XYZW", [1, 2, 3]))[..^1];
        source[4..8].AsSpan().Clear();
        BitConverter.TryWriteBytes(source.AsSpan(4), source.Length - 8);

        var edited = Edit(source, TagField.Title, "Title");

        Assert.Equal(source.AsSpan(12, source.Length - 12).ToArray(), edited.AsSpan(12, source.Length - 12).ToArray());
        Assert.Equal("Title", Read(edited).Values[TagField.Title]);
    }

    public static TheoryData<string, byte[], TagProblem> UnwritableFiles()
    {
        var valid = WavFixture.Build(WavFixture.Fmt(), WavFixture.Data());
        byte[] rf64 = [.. "RF64"u8, .. valid[4..]];
        var truncated = valid[..^100];
        var twoData = WavFixture.Build(WavFixture.Fmt(), WavFixture.Data(), WavFixture.Data(100));
        var twoId3 = WavFixture.Build(WavFixture.Fmt(), WavFixture.Data(), WavFixture.Id3([]), WavFixture.Id3([]));
        byte[] trailing = [.. valid, 1, 2, 3];
        return new()
        {
            { "RF64 (over 4 GB)", rf64, TagProblem.UnsupportedVersion },
            { "truncated", truncated, TagProblem.Malformed },
            { "two data chunks", twoData, TagProblem.Malformed },
            { "two id3 chunks", twoId3, TagProblem.Malformed },
            { "trailing bytes", trailing, TagProblem.Malformed },
        };
    }

    [Theory]
    [MemberData(nameof(UnwritableFiles))]
    [Trait("Req", "REQ-130")]
    public void Files_that_cannot_be_fully_parsed_are_refused_and_left_untouched(string _, byte[] source, TagProblem expected)
    {
        var sourcePath = WriteFile(source);
        var destination = Path.Combine(_dir, $"edited-{_counter++}.wav");

        var result = WavTagWriter.Rewrite(sourcePath, destination, new Dictionary<TagField, string?> { [TagField.Title] = "x" });

        Assert.Equal(expected, Assert.IsType<TagNotWritable>(result.Error).Problem);
        Assert.Equal(expected, Read(source).WriteProblem?.Problem);
        Assert.False(File.Exists(destination));
        Assert.Equal(source, File.ReadAllBytes(sourcePath));
    }

    [Theory]
    [InlineData(WriteStep.TempWritten, false)]
    [InlineData(WriteStep.Replaced, true)]
    [Trait("Req", "REQ-092")]
    public void Safe_write_of_a_wav_recovers_from_a_crash(WriteStep crashAfter, bool expectNew)
    {
        var path = Path.Combine(_dir, "track.wav");
        var original = FullFile();
        File.WriteAllBytes(path, original);
        var changes = new Dictionary<TagField, string?> { [TagField.Artist] = "Röyksopp", [TagField.Label] = "Label" };
        var expected = Edit(original, changes);
        var store = new FileRecoveryStore(Path.Combine(_dir, "recovery"));

        var writer = new SafeFileWriter(store) { AfterStep = step => { if (step == crashAfter) throw new OperationCanceledException(); } };
        Assert.Throws<OperationCanceledException>(() => writer.Write(path, changes));
        RecoveryReconciler.ReconcileAll(store);

        Assert.Equal(expectNew ? expected : original, File.ReadAllBytes(path));
        Assert.Empty(store.LoadAll());
    }

    [Fact]
    [Trait("Req", "REQ-003")]
    public void Safe_write_of_a_wav_succeeds()
    {
        var path = Path.Combine(_dir, "track.wav");
        File.WriteAllBytes(path, FullFile());

        var result = new SafeFileWriter(new FileRecoveryStore(Path.Combine(_dir, "recovery"))).Write(path, new Dictionary<TagField, string?> { [TagField.Genre] = "House" });

        Assert.True(result.IsSuccess, result.ToString());
        Assert.Equal("House", TagReader.Read(path).Values[TagField.Genre]);
    }
}
