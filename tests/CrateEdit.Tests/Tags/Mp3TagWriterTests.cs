using CrateEdit.Core.Tags;
using CrateEdit.Core.Tags.Id3;
using CrateEdit.Core.Tags.Writing;

namespace CrateEdit.Tests.Tags;

public class Mp3TagWriterTests : Mp3TagTestBase
{
    private static readonly Dictionary<TagField, string> V4FrameIds = new()
    {
        [TagField.Artist] = "TPE1",
        [TagField.Title] = "TIT2",
        [TagField.Album] = "TALB",
        [TagField.Genre] = "TCON",
        [TagField.Label] = "TPUB",
        [TagField.Bpm] = "TBPM",
        [TagField.Key] = "TKEY",
        [TagField.Comment] = "COMM",
        [TagField.Year] = "TDRC",
    };

    /// <summary>Frames that belong to DJ software, stores and players, never to CrateEdit.</summary>
    private static readonly string[] ForeignFrameIds = ["GEOB", "PRIV", "UFID", "TXXX", "APIC", "WOAF"];

    private const string StoreCommentDescription = "ID3v1 Comment";

    private static byte[] StoreFileWithDjData(int padding = 4096) =>
        Mp3Fixture.Build(4, [.. Frames.BeatportLike(), .. Frames.DjSoftwareData()], padding);

    public static TheoryData<TagField> AllFields() => new(Enum.GetValues<TagField>());

    [Theory]
    [MemberData(nameof(AllFields))]
    [Trait("Req", "REQ-088")]
    [Trait("Req", "REQ-130")]
    public void Editing_a_field_leaves_everything_else_byte_identical(TagField field)
    {
        // Arrange
        var source = StoreFileWithDjData();

        // Act
        var edited = Edit(source, field, "New value 1");

        // Assert
        AssertOnlyEdited(Mp3Fixture.Parse(source), Mp3Fixture.Parse(edited), V4FrameIds[field]);
    }

    [Theory]
    [MemberData(nameof(AllFields))]
    [Trait("Req", "REQ-130")]
    public void Edited_value_is_read_back(TagField field)
    {
        // Arrange
        var source = StoreFileWithDjData();

        // Act
        var edited = Edit(source, field, "New value 1");

        // Assert
        Assert.Equal("New value 1", ReadValue(edited, field));
    }

    [Fact]
    [Trait("Req", "REQ-088")]
    [Trait("Req", "REQ-130")]
    public void Editing_all_fields_preserves_dj_software_data_store_frames_and_cover()
    {
        // Arrange
        var source = StoreFileWithDjData();

        // Act
        var edited = Edit(source, EditEveryField());

        // Assert
        var before = Mp3Fixture.Parse(source);
        var after = Mp3Fixture.Parse(edited);
        AssertOnlyEdited(before, after, [.. V4FrameIds.Values]);
        AssertFramesUnchanged(before, after, ForeignFrameIds);
    }

    [Fact]
    [Trait("Req", "REQ-130")]
    public void Edited_frame_stays_in_place_and_new_frames_are_appended()
    {
        // Arrange
        var source = Mp3Fixture.Build(4, [Frames.Text("TIT2", "Title"), Frames.Text("TPE1", "Artist"), Frames.Picture()]);
        var changes = new Dictionary<TagField, string?>
        {
            [TagField.Artist] = "New",
            [TagField.Key] = "8A",
        };

        // Act
        var edited = Edit(source, changes);

        // Assert
        Assert.Equal(["TIT2", "TPE1", "APIC", "TKEY"], Mp3Fixture.Parse(edited).FrameIds);
    }

    [Fact]
    [Trait("Req", "REQ-130")]
    public void Edit_that_fits_in_the_padding_does_not_move_the_audio()
    {
        // Arrange
        var source = StoreFileWithDjData(padding: 4096);

        // Act
        var edited = Edit(source, TagField.Comment, "A somewhat longer comment than before");

        // Assert
        Assert.Equal(Mp3Fixture.Parse(source).TagSize, Mp3Fixture.Parse(edited).TagSize);
        Assert.Equal(source.Length, edited.Length);
    }

    [Fact]
    [Trait("Req", "REQ-088")]
    [Trait("Req", "REQ-130")]
    public void Edit_that_does_not_fit_grows_the_tag_and_keeps_audio_intact()
    {
        // Arrange
        var source = StoreFileWithDjData(padding: 0);
        var longComment = new string('x', 500);

        // Act
        var edited = Edit(source, TagField.Comment, longComment);

        // Assert
        var before = Mp3Fixture.Parse(source);
        var after = Mp3Fixture.Parse(edited);
        Assert.True(after.TagSize > before.TagSize);
        AssertOnlyEdited(before, after, "COMM");
    }

    [Fact]
    [Trait("Req", "REQ-130")]
    public void Clearing_a_field_removes_its_frame()
    {
        // Arrange
        var source = StoreFileWithDjData();

        // Act
        var edited = Edit(source, TagField.Label, null);

        // Assert
        var after = Mp3Fixture.Parse(edited);
        Assert.False(after.HasFrame("TPUB"));
        Assert.Null(ReadValue(edited, TagField.Label));
        AssertOnlyEdited(Mp3Fixture.Parse(source), after, "TPUB");
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
        // Arrange
        var source = Mp3Fixture.Build(version, [Frames.Text("TPE1", "Old", TextEncodings.Latin1)]);

        // Act
        var edited = Edit(source, TagField.Artist, value);

        // Assert
        Assert.Equal(value, ReadValue(edited, TagField.Artist));
    }

    [Theory]
    [InlineData("Röyksopp", TextEncodings.Latin1)]
    [InlineData("東京", TextEncodings.Utf16)]
    public void Id3v23_keeps_latin1_when_possible_and_switches_to_utf16_when_needed(string value, byte expectedEncoding)
    {
        // Arrange
        var source = Mp3Fixture.Build(3, [Frames.Text("TPE1", "Old", TextEncodings.Latin1)]);

        // Act
        var edited = Edit(source, TagField.Artist, value);

        // Assert
        Assert.Equal(expectedEncoding, Mp3Fixture.Parse(edited).Frame("TPE1").TextEncoding);
    }

    [Theory]
    [InlineData(3, "TYER")]
    [InlineData(4, "TDRC")]
    public void Year_is_written_to_the_frame_of_the_id3_version(int version, string expectedFrameId)
    {
        // Arrange
        var source = Mp3Fixture.Build(version, []);

        // Act
        var edited = Edit(source, TagField.Year, "1977");

        // Assert
        Assert.Equal([expectedFrameId], Mp3Fixture.Parse(edited).FrameIds);
    }

    [Fact]
    [Trait("Req", "REQ-088")]
    public void File_without_id3v2_tag_gets_an_id3v23_tag_and_unchanged_audio()
    {
        // Arrange
        var source = Mp3Fixture.BuildWithoutId3v2();

        // Act
        var edited = Edit(source, TagField.Title, "Title");

        // Assert
        var after = Mp3Fixture.Parse(edited);
        Assert.Equal(3, after.Version);
        Assert.Equal(Mp3Fixture.Parse(source).Audio, after.Audio);
        Assert.Equal("Title", ReadValue(edited, TagField.Title));
    }

    [Fact]
    [Trait("Req", "REQ-130")]
    public void Existing_id3v1_tag_is_updated()
    {
        // Arrange
        const byte BluesGenre = 0;
        const byte HouseGenre = 35;
        var id3v1 = Frames.Id3v1("Old title", "Old artist", genre: BluesGenre);
        var source = Mp3Fixture.Build(4, [Frames.Text("TIT2", "Old")], id3v1: id3v1);
        var changes = new Dictionary<TagField, string?>
        {
            [TagField.Title] = "New title",
            [TagField.Artist] = "New artist",
            [TagField.Genre] = "house",
        };

        // Act
        var edited = Edit(source, changes);

        // Assert
        var tag = Mp3Fixture.Parse(edited).ParsedId3v1;
        Assert.Equal("New title", tag.Title);
        Assert.Equal("New artist", tag.Artist);
        Assert.Equal(HouseGenre, tag.Genre);
    }

    [Fact]
    [Trait("Req", "REQ-130")]
    public void Id3v1_title_is_cut_to_30_characters()
    {
        // Arrange
        var source = Mp3Fixture.Build(4, [], id3v1: Frames.Id3v1("Old title", "Old artist"));

        // Act
        var edited = Edit(source, TagField.Title, "A title that is much longer than thirty characters");

        // Assert
        Assert.Equal("A title that is much longer th", Mp3Fixture.Parse(edited).ParsedId3v1.Title);
    }

    [Fact]
    [Trait("Req", "REQ-130")]
    public void Characters_that_id3v1_cannot_store_become_question_marks()
    {
        // Arrange
        var source = Mp3Fixture.Build(4, [], id3v1: Frames.Id3v1("Old title", "Old artist"));

        // Act
        var edited = Edit(source, TagField.Artist, "坂本");

        // Assert
        Assert.Equal("??", Mp3Fixture.Parse(edited).ParsedId3v1.Artist);
    }

    [Fact]
    [Trait("Req", "REQ-130")]
    public void Id3v1_keeps_its_track_number()
    {
        // Arrange
        const byte TrackNumber = 7;
        var source = Mp3Fixture.Build(4, [], id3v1: Frames.Id3v1("Title", "Artist", track: TrackNumber));

        // Act
        var edited = Edit(source, TagField.Title, "New title");

        // Assert
        Assert.Equal(TrackNumber, Mp3Fixture.Parse(edited).ParsedId3v1.Track);
    }

    [Fact]
    [Trait("Req", "REQ-130")]
    public void Id3v1_tag_is_never_added()
    {
        // Arrange
        var source = Mp3Fixture.Build(4, [Frames.Text("TIT2", "Old")]);

        // Act
        var edited = Edit(source, TagField.Title, "New");

        // Assert
        Assert.Null(Mp3Fixture.Parse(edited).Id3v1);
    }

    [Fact]
    [Trait("Req", "REQ-006")]
    public void Comment_is_the_comment_frame_without_a_description()
    {
        // Arrange
        var source = Mp3Fixture.Build(4,
        [
            Frames.Comment(StoreCommentDescription, "Purchased at Beatport"),
            Frames.Comment("", "Mine"),
        ]);

        // Act
        var comment = ReadValue(source, TagField.Comment);

        // Assert
        Assert.Equal("Mine", comment);
    }

    [Fact]
    [Trait("Req", "REQ-006")]
    public void Store_comment_is_not_shown_as_the_comment()
    {
        // Rekordbox and Serato show only the comment without a description (manual tests, ADR-0012).

        // Arrange
        var source = Mp3Fixture.Build(4, [Frames.Comment(StoreCommentDescription, "Purchased at Beatport")]);

        // Act
        var comment = ReadValue(source, TagField.Comment);

        // Assert
        Assert.Null(comment);
    }

    [Fact]
    [Trait("Req", "REQ-006")]
    public void Editing_the_comment_changes_only_the_frame_without_a_description()
    {
        // Arrange
        var storeComment = Frames.Comment(StoreCommentDescription, "Purchased at Beatport");
        var source = Mp3Fixture.Build(4, [storeComment, Frames.Comment("", "Mine")]);

        // Act
        var edited = Edit(source, TagField.Comment, "Edited");

        // Assert
        Assert.Equal(storeComment.Body, Mp3Fixture.Parse(edited).Frames[0].Body);
        Assert.Equal("Edited", ReadValue(edited, TagField.Comment));
    }

    [Fact]
    [Trait("Req", "REQ-006")]
    public void Editing_the_comment_of_a_file_with_only_a_store_comment_adds_a_plain_comment()
    {
        // Arrange
        var storeComment = Frames.Comment(StoreCommentDescription, "Purchased at Beatport", TextEncodings.Latin1);
        var source = Mp3Fixture.Build(4, [storeComment]);
        var expectedComment = Frames.Comment("", "Edited", TextEncodings.Latin1, terminated: false);

        // Act
        var edited = Edit(source, TagField.Comment, "Edited");

        // Assert
        var after = Mp3Fixture.Parse(edited);
        Assert.Equal(storeComment.Body, after.Frames[0].Body);
        Assert.Equal(expectedComment.Body, after.Frames[1].Body);
    }

    [Fact]
    [Trait("Req", "REQ-088")]
    public void Technical_itunes_comment_is_not_shown_as_the_comment()
    {
        // Arrange
        var source = Mp3Fixture.Build(3, [Frames.Comment("iTunNORM", " 00000A2B 00000B3C", TextEncodings.Latin1)]);

        // Act
        var comment = ReadValue(source, TagField.Comment);

        // Assert
        Assert.Null(comment);
    }

    [Fact]
    [Trait("Req", "REQ-088")]
    public void Editing_the_comment_leaves_technical_itunes_comments_unchanged()
    {
        // Arrange
        var itunesComment = Frames.Comment("iTunNORM", " 00000A2B 00000B3C", TextEncodings.Latin1);
        var source = Mp3Fixture.Build(3, [itunesComment]);

        // Act
        var edited = Edit(source, TagField.Comment, "Mine");

        // Assert
        var after = Mp3Fixture.Parse(edited);
        Assert.Equal(2, after.Frames.Count);
        Assert.Equal(itunesComment.Body, after.Frames[0].Body);
    }

    [Fact]
    public void Reads_store_values_with_and_without_terminators()
    {
        // Arrange
        var source = Mp3Fixture.Build(4,
        [
            Frames.Text("TPE1", "Heatwave", terminated: false),
            Frames.Text("TBPM", "128.00"),
            Frames.Text("TKEY", "Emin", TextEncodings.Latin1),
        ]);

        // Act
        var values = Read(source).Values;

        // Assert
        Assert.Equal("Heatwave", values[TagField.Artist]);
        Assert.Equal("128.00", values[TagField.Bpm]);
        Assert.Equal("Emin", values[TagField.Key]);
    }

    [Fact]
    public void Multiple_id3v24_values_are_read_comma_separated()
    {
        // Arrange
        var source = Mp3Fixture.Build(4, [Frames.MultiValueText("TPE1", "DJ Chus", "Harry Romero")]);

        // Act
        var artist = ReadValue(source, TagField.Artist);

        // Assert
        Assert.Equal("DJ Chus, Harry Romero", artist);
    }

    [Fact]
    [Trait("Req", "REQ-088")]
    public void Unsynchronised_tag_is_reported_as_not_writable()
    {
        // Arrange
        const byte UnsynchronisationFlag = 0x80;
        var source = Mp3Fixture.Build(3, [Frames.Text("TIT2", "Title", TextEncodings.Latin1)], flags: UnsynchronisationFlag);

        // Act
        var tags = Read(source);

        // Assert
        Assert.Equal(TagProblem.UnsupportedFeature, tags.WriteProblem?.Problem);
    }

    [Fact]
    [Trait("Req", "REQ-088")]
    public void Unsynchronised_tag_is_not_edited()
    {
        // Arrange
        const byte UnsynchronisationFlag = 0x80;
        var source = Mp3Fixture.Build(3, [Frames.Text("TIT2", "Title", TextEncodings.Latin1)], flags: UnsynchronisationFlag);

        // Act
        var refusal = EditRefused(source, TagField.Title);

        // Assert
        Assert.Equal(TagProblem.UnsupportedFeature, refusal.Problem);
    }

    [Fact]
    [Trait("Req", "REQ-088")]
    public void Id3v22_tag_is_reported_as_unsupported_version()
    {
        // Arrange
        var source = Mp3Fixture.Build(2, []);

        // Act
        var tags = Read(source);

        // Assert
        Assert.Equal(new TagNotWritable(TagProblem.UnsupportedVersion, Version: 2), tags.WriteProblem);
    }

    [Fact]
    [Trait("Req", "REQ-088")]
    public void Id3v22_tag_is_not_edited()
    {
        // Arrange
        var source = Mp3Fixture.Build(2, []);

        // Act
        var refusal = EditRefused(source, TagField.Title);

        // Assert
        Assert.Equal(TagProblem.UnsupportedVersion, refusal.Problem);
    }

    [Fact]
    [Trait("Req", "REQ-088")]
    public void Unrecognised_data_in_the_padding_makes_the_tag_not_writable()
    {
        // Arrange
        var source = Mp3Fixture.WithDataInPadding(Mp3Fixture.Build(4, [Frames.Text("TIT2", "Title")], padding: 64));

        // Act
        var tags = Read(source);

        // Assert
        Assert.Equal(TagProblem.Malformed, tags.WriteProblem?.Problem);
    }

    [Fact]
    [Trait("Req", "REQ-088")]
    public void Unrecognised_data_in_the_padding_is_refused_with_its_position()
    {
        // Arrange
        var source = Mp3Fixture.WithDataInPadding(Mp3Fixture.Build(4, [Frames.Text("TIT2", "Title")], padding: 64));

        // Act
        var refusal = EditRefused(source, TagField.Title);

        // Assert
        Assert.Equal(TagProblem.Malformed, refusal.Problem);
        Assert.NotNull(refusal.Offset);
    }

    [Fact]
    [Trait("Req", "REQ-088")]
    public void Compressed_frame_of_an_edited_field_is_refused()
    {
        // Arrange
        var source = Mp3Fixture.Build(4, [Frames.Compressed(Frames.Text("TPE1", "Artist"))]);

        // Act
        var refusal = EditRefused(source, TagField.Artist);

        // Assert
        Assert.Equal(TagProblem.EncodedFrame, refusal.Problem);
        Assert.Equal("TPE1", refusal.FrameId);
        Assert.Equal(TagField.Artist, refusal.Field);
    }

    [Fact]
    [Trait("Req", "REQ-088")]
    public void Compressed_frame_of_another_field_is_kept_unchanged()
    {
        // Arrange
        var compressed = Frames.Compressed(Frames.Text("TPE1", "Artist"));
        var source = Mp3Fixture.Build(4, [compressed, Frames.Text("TIT2", "Title")]);

        // Act
        var edited = Edit(source, TagField.Title, "New title");

        // Assert
        Assert.True(compressed.SameBytesAs(Mp3Fixture.Parse(edited).Frames[0]));
    }

    [Fact]
    [Trait("Req", "REQ-088")]
    public void Duplicate_frames_of_an_edited_field_are_refused()
    {
        // Arrange
        var source = Mp3Fixture.Build(3, [Frames.Text("TPE1", "One", TextEncodings.Latin1), Frames.Text("TPE1", "Two", TextEncodings.Latin1)]);

        // Act
        var refusal = EditRefused(source, TagField.Artist);

        // Assert
        Assert.Equal(TagProblem.DuplicateFrame, refusal.Problem);
        Assert.Equal("TPE1", refusal.FrameId);
        Assert.Equal(TagField.Artist, refusal.Field);
    }

    [Fact]
    [Trait("Req", "REQ-088")]
    public void Duplicate_frames_leave_the_other_fields_writable()
    {
        // Arrange
        var source = Mp3Fixture.Build(3, [Frames.Text("TPE1", "One", TextEncodings.Latin1), Frames.Text("TPE1", "Two", TextEncodings.Latin1)]);

        // Act
        var tags = Read(source);

        // Assert
        Assert.True(tags.CanWrite);
    }

    [Theory]
    [InlineData(0, "Blues")]
    [InlineData(35, "House")]
    [InlineData(79, "Hard Rock")]
    public void Id3v1_genre_list_has_the_80_original_genres(int number, string expectedName)
    {
        // Act
        var genres = Id3v1Tag.Genres;

        // Assert
        Assert.Equal(80, genres.Length);
        Assert.Equal(expectedName, genres[number]);
    }
}
