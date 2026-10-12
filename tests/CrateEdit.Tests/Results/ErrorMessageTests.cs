using System.Globalization;
using CrateEdit.App.Errors;
using CrateEdit.App.Resources;
using CrateEdit.App.Tags;
using CrateEdit.Core.Results;
using CrateEdit.Core.Tags;

namespace CrateEdit.Tests.Results;

/// <summary>Every error the application can report has a user-facing message in each language (REQ-123).</summary>
[Trait("Req", "REQ-123")]
public class ErrorMessageTests
{
    public static TheoryData<string, string> CodesAndLanguages() =>
        new(from code in ErrorCodes.All from language in new[] { "en", "de" } select (code, language));

    [Theory]
    [MemberData(nameof(CodesAndLanguages))]
    public void Every_error_code_has_a_message(string code, string language)
    {
        var message = Strings.ResourceManager.GetString(ErrorMessages.ResourceKey(code), CultureInfo.GetCultureInfo(language));

        Assert.False(string.IsNullOrWhiteSpace(message), $"No {language} message for error code '{code}'.");
    }

    public static TheoryData<TagProblem> TagProblems() => new(Enum.GetValues<TagProblem>());

    [Theory]
    [MemberData(nameof(TagProblems))]
    public void Every_tag_problem_has_a_declared_error_code(TagProblem problem)
    {
        Assert.Contains(new TagNotWritable(problem).Code, ErrorCodes.All);
    }

    [Fact]
    public void Message_is_shown_in_the_display_language()
    {
        var original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");
            Assert.StartsWith("Die Tags der Datei sind beschädigt", ErrorMessages.For(new TagNotWritable(TagProblem.Malformed)));
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }

    [Fact]
    public void Message_names_the_field_as_shown_in_the_ui_and_no_technical_details()
    {
        var error = new TagNotWritable(TagProblem.EncodedFrame, FrameId: "TPE1", Offset: 120)
        {
            Field = TagField.Artist,
            Cause = new IOException("Sharing violation at 0x8007"),
        };

        var message = InCulture("en-GB", () => ErrorMessages.For(error));

        Assert.Contains("Artist", message);
        Assert.DoesNotContain("TPE1", message);
        Assert.DoesNotContain("120", message);
        Assert.DoesNotContain("Sharing violation", message);
    }

    [Fact]
    public void German_message_uses_the_german_field_name()
    {
        var error = new TagNotWritable(TagProblem.DuplicateFrame, FrameId: "TPE1") { Field = TagField.Artist };

        Assert.StartsWith("„Interpret“ ist in dieser Datei zweimal eingetragen", InCulture("de-DE", () => ErrorMessages.For(error)));
    }

    [Fact]
    public void Missing_detail_falls_back_to_the_generic_message()
    {
        var message = InCulture("en-GB", () => ErrorMessages.For(new TagNotWritable(TagProblem.DuplicateFrame)));

        Assert.Contains(ErrorCodes.TagsDuplicateFrame, message);
    }

    public static TheoryData<TagField, string> FieldsAndLanguages() =>
        new(from field in Enum.GetValues<TagField>() from language in new[] { "en", "de" } select (field, language));

    [Theory]
    [MemberData(nameof(FieldsAndLanguages))]
    public void Every_field_has_a_display_name(TagField field, string language)
    {
        var name = Strings.ResourceManager.GetString(FieldNames.ResourceKey(field), CultureInfo.GetCultureInfo(language));

        Assert.False(string.IsNullOrWhiteSpace(name), $"No {language} name for field {field}.");
    }

    private static string InCulture(string culture, Func<string> action)
    {
        var original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            return action();
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }
}
