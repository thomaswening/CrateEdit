using System.Globalization;
using CrateEdit.App.Errors;
using CrateEdit.App.Resources;
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
    public void Message_contains_no_technical_details()
    {
        var message = ErrorMessages.For(new TagNotWritable(TagProblem.EncodedFrame, FrameId: "TPE1", Offset: 120));

        Assert.DoesNotContain("TPE1", message);
        Assert.DoesNotContain("120", message);
    }
}
