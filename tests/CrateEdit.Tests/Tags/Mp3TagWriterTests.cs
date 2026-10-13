using CrateEdit.Core.Tags;
using CrateEdit.Core.Tags.Writing;

namespace CrateEdit.Tests.Tags;

public class Mp3TagWriterTests : Mp3TagTestBase
{
    private static readonly Dictionary<TagField, string> V4FrameIds = new()
    {
        [TagField.Artist] = "TPE1", [TagField.Title] = "TIT2", [TagField.Album] = "TALB",
        [TagField.Genre] = "TCON", [TagField.Label] = "TPUB", [TagField.Bpm] = "TBPM",
        [TagField.Key] = "TKEY", [TagField.Comment] = "COMM", [TagField.Year] = "TDRC",
    };

    private static byte[] StoreFileWithDjData(int version = 4, int padding = 4096) =>
        Mp3Fixture.Build(version, [.. Frames.BeatportLike(), .. Frames.DjSoftwareData()], padding);

    public static TheoryData<TagField> AllFields() => new(Enum.GetValues<TagField>());

    [Theory]
    [MemberData(nameof(AllFields))]
    [Trait("Req", "REQ-088")]
    [Trait("Req", "REQ-130")]
    public void Editing_a_field_leaves_everything_else_byte_identical(TagField field)
    {
        var source = StoreFileWithDjData();

        var edited = Edit(source, field, "New value 1");

        AssertOnlyEdited(Mp3Fixture.Parse(source), Mp3Fixture.Parse(edited), V4FrameIds[field]);
        Assert.Equal("New value 1", Read(edited).Values[field]);
    }

    [Fact]
    [Trait("Req", "REQ-088")]
    [Trait("Req", "REQ-130")]
    public void Editing_all_fields_preserves_dj_software_data_store_frames_and_cover()
    {
        var source = StoreFileWithDjData();
        var changes = Enum.GetValues<TagField>().ToDictionary(f => f, f => (string?)$"Edited {f}");

        var edited = Edit(source, changes);

        var before = Mp3Fixture.Parse(source);
        var after = Mp3Fixture.Parse(edited);
        AssertOnlyEdited(before, after, [.. V4FrameIds.Values]);
        foreach (var id in new[] { "GEOB", "PRIV", "UFID", "TXXX", "APIC", "WOAF" })
        {
            Assert.Equal(before.Frames.Where(f => f.Id == id).Select(f => f.Body), after.Frames.Where(f => f.Id == id).Select(f => f.Body));
        }
    }

    [Fact]
    [Trait("Req", "REQ-130")]
    public void Edited_frame_stays_in_place_and_new_frames_are_appended()
    {
        var source = Mp3Fixture.Build(4, [Frames.Text("TIT2", "Title"), Frames.Text("TPE1", "Artist"), Frames.Picture()]);

        var after = Mp3Fixture.Parse(Edit(source, new Dictionary<TagField, string?> { [TagField.Artist] = "New", [TagField.Key] = "8A" }));

        Assert.Equal(["TIT2", "TPE1", "APIC", "TKEY"], after.Frames.Select(f => f.Id));
    }

    [Fact]
    [Trait("Req", "REQ-130")]
    public void Edit_that_fits_in_the_padding_does_not_move_the_audio()
    {
        var source = StoreFileWithDjData(padding: 4096);

        var edited = Edit(source, TagField.Comment, "A somewhat longer comment than before");

        Assert.Equal(Mp3Fixture.Parse(source).TagSize, Mp3Fixture.Parse(edited).TagSize);
        Assert.Equal(source.Length, edited.Length);
    }

    [Fact]
    [Trait("Req", "REQ-088")]
    [Trait("Req", "REQ-130")]
    public void Edit_that_does_not_fit_grows_the_tag_and_keeps_audio_intact()
    {
        var source = StoreFileWithDjData(padding: 0);

        var edited = Edit(source, TagField.Comment, new string('x', 500));

        var before = Mp3Fixture.Parse(source);
        var after = Mp3Fixture.Parse(edited);
        Assert.True(after.TagSize > before.TagSize);
        AssertOnlyEdited(before, after, "COMM");
    }

    [Fact]
    [Trait("Req", "REQ-130")]
    public void Clearing_a_field_removes_its_frame()
    {
        var source = StoreFileWithDjData();

        var edited = Edit(source, TagField.Label, null);

        Assert.DoesNotContain(Mp3Fixture.Parse(edited).Frames, f => f.Id == "TPUB");
        Assert.Null(Read(edited).Values[TagField.Label]);
        AssertOnlyEdited(Mp3Fixture.Parse(source), Mp3Fixture.Parse(edited), "TPUB");
    }

    [Theory]
    [Trait("Req", "REQ-007")]
    [InlineData(3, "Röyksopp")]
    [InlineData(3, "坂本龍一")]
    [InlineData(4, "Röyksopp")]
    [InlineData(4, "坂本龍一")]
    [InlineData(4, "Ärzte, Beyoncé, Sigur Rós")]
    public void Values_round_trip_in_both_id3_versions(int version, string value)
    {
        var source = Mp3Fixture.Build(version, [Frames.Text("TPE1", "Old", encoding: 0)]);

        var edited = Edit(source, TagField.Artist, value);

        Assert.Equal(value, Read(edited).Values[TagField.Artist]);
    }

    [Fact]
    public void Keeps_latin1_encoding_when_possible_and_switches_when_needed()
    {
        var source = Mp3Fixture.Build(3, [Frames.Text("TPE1", "Old", encoding: 0), Frames.Text("TIT2", "Old", encoding: 0)]);

        var after = Mp3Fixture.Parse(Edit(source, new Dictionary<TagField, string?> { [TagField.Artist] = "Röyksopp", [TagField.Title] = "東京" }));

        Assert.Equal(0, after.Frames.Single(f => f.Id == "TPE1").Body[0]);
        Assert.Equal(1, after.Frames.Single(f => f.Id == "TIT2").Body[0]);
    }

    [Fact]
    public void Year_uses_tyer_in_id3v23_and_tdrc_in_id3v24()
    {
        var v3 = Mp3Fixture.Parse(Edit(Mp3Fixture.Build(3, []), TagField.Year, "1977"));
        var v4 = Mp3Fixture.Parse(Edit(Mp3Fixture.Build(4, []), TagField.Year, "1977"));

        Assert.Equal("TYER", Assert.Single(v3.Frames).Id);
        Assert.Equal("TDRC", Assert.Single(v4.Frames).Id);
    }

    [Fact]
    [Trait("Req", "REQ-088")]
    public void File_without_id3v2_tag_gets_an_id3v23_tag_and_unchanged_audio()
    {
        var source = Mp3Fixture.BuildWithoutId3v2();

        var edited = Edit(source, TagField.Title, "Title");

        var after = Mp3Fixture.Parse(edited);
        Assert.Equal(3, after.Version);
        Assert.Equal(Mp3Fixture.Parse(source).Audio, after.Audio);
        Assert.Equal("Title", Read(edited).Values[TagField.Title]);
    }

    [Fact]
    [Trait("Req", "REQ-130")]
    public void Existing_id3v1_is_updated_and_never_added()
    {
        var withV1 = Mp3Fixture.Build(4, [Frames.Text("TIT2", "Old")], id3v1: Frames.Id3v1("Old title", "Old artist"));
        var withoutV1 = Mp3Fixture.Build(4, [Frames.Text("TIT2", "Old")]);

        var editedV1 = Mp3Fixture.Parse(Edit(withV1, new Dictionary<TagField, string?>
        {
            [TagField.Title] = "A title that is much longer than thirty characters",
            [TagField.Artist] = "坂本",
            [TagField.Genre] = "house",
            [TagField.Label] = "Not in ID3v1",
        })).Id3v1!;
        var editedNoV1 = Mp3Fixture.Parse(Edit(withoutV1, TagField.Title, "New"));

        Assert.Equal("A title that is much longer th", System.Text.Encoding.Latin1.GetString(editedV1, 3, 30));
        Assert.Equal("??", System.Text.Encoding.Latin1.GetString(editedV1, 33, 30).TrimEnd('\0'));
        Assert.Equal(35, editedV1[127]);
        Assert.Equal(1, editedV1[126]);
        Assert.Null(editedNoV1.Id3v1);
    }

    [Fact]
    public void Comment_without_description_is_preferred()
    {
        var source = Mp3Fixture.Build(4, [Frames.Comment("ID3v1 Comment", "Store"), Frames.Comment("", "Mine")]);

        Assert.Equal("Mine", Read(source).Values[TagField.Comment]);
        var after = Mp3Fixture.Parse(Edit(source, TagField.Comment, "Edited"));
        Assert.Equal(Frames.Comment("ID3v1 Comment", "Store").Body, after.Frames[0].Body);
    }

    [Fact]
    [Trait("Req", "REQ-006")]
    public void Store_comment_is_neither_shown_nor_changed_and_a_plain_comment_is_added()
    {
        // Rekordbox and Serato show only the comment without a description (manual tests, ADR-0012).
        var store = Frames.Comment("ID3v1 Comment", "Purchased at Beatport", encoding: 0);
        var source = Mp3Fixture.Build(4, [store]);

        Assert.Null(Read(source).Values[TagField.Comment]);
        var edited = Edit(source, TagField.Comment, "Edited");
        var after = Mp3Fixture.Parse(edited);
        Assert.Equal(store.Body, after.Frames[0].Body);
        Assert.Equal(Frames.Comment("", "Edited", encoding: 0).Body[..^1], after.Frames[1].Body);
        Assert.Equal("Edited", Read(edited).Values[TagField.Comment]);
    }

    [Fact]
    [Trait("Req", "REQ-088")]
    public void Technical_itunes_comments_are_never_used_or_changed()
    {
        var itunes = Frames.Comment("iTunNORM", " 00000A2B 00000B3C");
        var source = Mp3Fixture.Build(3, [itunes], id3v1: null);

        Assert.Null(Read(source).Values[TagField.Comment]);
        var after = Mp3Fixture.Parse(Edit(source, TagField.Comment, "Mine"));
        Assert.Equal(itunes.Body, after.Frames[0].Body);
        Assert.Equal(2, after.Frames.Count);
    }

    [Fact]
    public void Reads_store_values_with_and_without_terminators()
    {
        var source = Mp3Fixture.Build(4,
        [
            Frames.Text("TPE1", "Heatwave", terminated: false),
            Frames.Text("TBPM", "128.00"),
            Frames.Text("TKEY", "Emin", encoding: 0),
        ]);

        var values = Read(source).Values;

        Assert.Equal("Heatwave", values[TagField.Artist]);
        Assert.Equal("128.00", values[TagField.Bpm]);
        Assert.Equal("Emin", values[TagField.Key]);
    }

    [Fact]
    public void Multiple_id3v24_values_are_read_comma_separated()
    {
        var source = Mp3Fixture.Build(4, [new RawFrame("TPE1", 0, [3, .. "DJ Chus"u8, 0, .. "Harry Romero"u8])]);

        Assert.Equal("DJ Chus, Harry Romero", Read(source).Values[TagField.Artist]);
    }

    [Fact]
    [Trait("Req", "REQ-088")]
    public void Unsynchronised_tag_is_reported_as_not_writable_and_left_untouched()
    {
        var source = Mp3Fixture.Build(3, [Frames.Text("TIT2", "Title", encoding: 0)], flags: 0x80);

        Assert.Equal(TagProblem.UnsupportedFeature, Read(source).WriteProblem?.Problem);
        AssertRefused(source, TagField.Title, TagProblem.UnsupportedFeature);
    }

    [Fact]
    [Trait("Req", "REQ-088")]
    public void Id3v22_is_reported_as_unsupported_version()
    {
        var source = Mp3Fixture.Build(4, [Frames.Text("TIT2", "Title")]);
        source[3] = 2;

        Assert.Equal(new TagNotWritable(TagProblem.UnsupportedVersion, Version: 2), Read(source).WriteProblem);
        AssertRefused(source, TagField.Title, TagProblem.UnsupportedVersion);
    }

    [Fact]
    [Trait("Req", "REQ-088")]
    public void Unrecognised_data_after_the_frames_makes_the_tag_not_writable()
    {
        var source = Mp3Fixture.Build(4, [Frames.Text("TIT2", "Title")], padding: 64);
        var tagEnd = Mp3Fixture.Parse(source).TagSize + 10;
        source[tagEnd - 10] = 0x42;

        Assert.Equal(TagProblem.Malformed, Read(source).WriteProblem?.Problem);
        var error = AssertRefused(source, TagField.Title, TagProblem.Malformed);
        Assert.NotNull(error.Offset);
    }

    [Fact]
    [Trait("Req", "REQ-088")]
    public void Compressed_frame_of_an_edited_field_is_refused()
    {
        var compressed = Frames.Text("TPE1", "Artist") with { Flags = 0x0008 };
        var source = Mp3Fixture.Build(4, [compressed, Frames.Text("TIT2", "Title")]);

        var error = AssertRefused(source, TagField.Artist, TagProblem.EncodedFrame);
        Assert.Equal(("TPE1", TagField.Artist), (error.FrameId, error.Field));
        var edited = Edit(source, TagField.Title, "New title");
        Assert.True(compressed.SameBytesAs(Mp3Fixture.Parse(edited).Frames[0]));
    }

    [Fact]
    [Trait("Req", "REQ-088")]
    public void Duplicate_frames_of_an_edited_field_are_refused()
    {
        var source = Mp3Fixture.Build(3, [Frames.Text("TPE1", "One", encoding: 0), Frames.Text("TPE1", "Two", encoding: 0)]);

        var error = AssertRefused(source, TagField.Artist, TagProblem.DuplicateFrame);
        Assert.Equal(("TPE1", TagField.Artist), (error.FrameId, error.Field));
        Assert.True(Read(source).CanWrite);
    }

    [Fact]
    public void Id3v1_genre_list_has_the_80_original_genres()
    {
        Assert.Equal(80, CrateEdit.Core.Tags.Id3.Id3v1Tag.Genres.Length);
        Assert.Equal("House", CrateEdit.Core.Tags.Id3.Id3v1Tag.Genres[35]);
        Assert.Equal("Hard Rock", CrateEdit.Core.Tags.Id3.Id3v1Tag.Genres[79]);
    }
}
