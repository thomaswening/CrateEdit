using System.Globalization;
using CrateEdit.App.Errors;
using CrateEdit.App.Resources;
using CrateEdit.App.Tags;
using CrateEdit.Core.Files;
using CrateEdit.Core.Results;
using CrateEdit.Core.Tags;

namespace CrateEdit.Tests.Results;

/// <summary>Every error the application can report has a user-facing message in each language (<see href="https://github.com/thomaswening/CrateEdit/blob/main/docs/requirements.md#req-123">REQ-123</see>).</summary>
[Trait("Req", "REQ-123")]
public class ErrorMessageTests
{
    private static readonly string[] Languages = ["en", "de"];

    public static TheoryData<string, string> CodesAndLanguages() =>
        new(from code in ErrorCodes.All from language in Languages select (code, language));

    public static TheoryData<TagField, string> FieldsAndLanguages() =>
        new(from field in Enum.GetValues<TagField>() from language in Languages select (field, language));

    public static TheoryData<TagProblem> TagProblems() => new(Enum.GetValues<TagProblem>());

    private static string? ResourceString(string key, string language) =>
        Strings.ResourceManager.GetString(key, CultureInfo.GetCultureInfo(language));

    [Theory]
    [MemberData(nameof(CodesAndLanguages))]
    public void Every_error_code_has_a_message(string code, string language)
    {
        // Act
        var message = ResourceString(ErrorMessages.ResourceKey(code), language);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(message), $"No {language} message for error code '{code}'.");
    }

    [Theory]
    [MemberData(nameof(FieldsAndLanguages))]
    public void Every_field_has_a_display_name(TagField field, string language)
    {
        // Act
        var name = ResourceString(FieldNames.ResourceKey(field), language);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(name), $"No {language} name for field {field}.");
    }

    [Theory]
    [MemberData(nameof(TagProblems))]
    public void Every_tag_problem_has_a_declared_error_code(TagProblem problem)
    {
        // Act
        var code = new TagNotWritable(problem).Code;

        // Assert
        Assert.Contains(code, ErrorCodes.All);
    }

    [Fact]
    public void Message_is_shown_in_the_display_language()
    {
        // Arrange
        using var german = CultureScope.Ui("de-DE");

        // Act
        var message = ErrorMessages.For(new TagNotWritable(TagProblem.Malformed));

        // Assert
        Assert.StartsWith("Die Tags der Datei sind beschädigt", message);
    }

    [Fact]
    public void Message_names_the_field_as_shown_in_the_ui_and_no_technical_details()
    {
        // Arrange
        using var english = CultureScope.Ui("en-GB");
        var error = new TagNotWritable(TagProblem.EncodedFrame, FrameId: "TPE1", Offset: 120)
        {
            Field = TagField.Artist,
            Cause = new IOException("Sharing violation at 0x8007"),
        };

        // Act
        var message = ErrorMessages.For(error);

        // Assert
        Assert.Contains("Artist", message);
        Assert.DoesNotContain("TPE1", message);
        Assert.DoesNotContain("120", message);
        Assert.DoesNotContain("Sharing violation", message);
    }

    [Fact]
    public void German_message_uses_the_german_field_name()
    {
        // Arrange
        using var german = CultureScope.Ui("de-DE");
        var error = new TagNotWritable(TagProblem.DuplicateFrame, FrameId: "TPE1") { Field = TagField.Artist };

        // Act
        var message = ErrorMessages.For(error);

        // Assert
        Assert.StartsWith("„Interpret“ ist in dieser Datei zweimal eingetragen", message);
    }

    [Theory]
    [InlineData("en-GB", "The file is open in rekordbox, VLC media player,")]
    [InlineData("de-DE", "Die Datei ist in rekordbox, VLC media player geöffnet")]
    public void Locked_file_message_names_the_programs_when_known(string language, string expectedStart)
    {
        // Arrange
        using var culture = CultureScope.Ui(language);
        var error = new FileAccessError(FileProblem.Locked, "track.mp3") { LockedBy = ["rekordbox", "VLC media player"] };

        // Act
        var message = ErrorMessages.For(error);

        // Assert
        Assert.StartsWith(expectedStart, message);
    }

    [Fact]
    public void Locked_file_message_without_known_programs_says_another_program()
    {
        // Arrange
        using var english = CultureScope.Ui("en-GB");
        var error = new FileAccessError(FileProblem.Locked, "track.mp3");

        // Act
        var message = ErrorMessages.For(error);

        // Assert
        Assert.StartsWith("The file is open in another program,", message);
    }

    [Fact]
    public void Missing_detail_falls_back_to_the_generic_message()
    {
        // Arrange
        using var english = CultureScope.Ui("en-GB");
        var errorWithoutField = new TagNotWritable(TagProblem.DuplicateFrame);

        // Act
        var message = ErrorMessages.For(errorWithoutField);

        // Assert
        Assert.Contains(ErrorCodes.TagsDuplicateFrame, message);
    }
}
