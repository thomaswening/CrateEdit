using System.Globalization;
using CrateEdit.Core.Results;

namespace CrateEdit.Tests.Results;

public class ResultTests
{
    private sealed record TestError(bool Transient = false) : Error
    {
        public override string Code => "test.error";

        public override bool IsTransient => Transient;
    }

    private static Result<int> Parse(string text) =>
        int.TryParse(text, out var value) ? value : new TestError();

    private static string Describe(Result<int> result) => result.Match(value => value.ToString(CultureInfo.InvariantCulture), error => error.Code);

    [Fact]
    public void Value_converts_to_a_successful_result()
    {
        // Act
        var result = Parse("42");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Error_converts_to_a_failed_result()
    {
        // Act
        var result = Parse("forty-two");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("test.error", result.Error.Code);
    }

    [Fact]
    public void Reading_the_value_of_a_failure_is_a_programming_error()
    {
        // Arrange
        var failure = Parse("forty-two");

        // Act and assert
        Assert.Throws<InvalidOperationException>(() => failure.Value);
    }

    [Theory]
    [InlineData("42", "42")]
    [InlineData("forty-two", "test.error")]
    public void Match_selects_the_branch(string text, string expected)
    {
        // Arrange
        var result = Parse(text);

        // Act
        var description = Describe(result);

        // Assert
        Assert.Equal(expected, description);
    }

    [Fact]
    public void Non_generic_success_has_no_error()
    {
        // Act
        Result result = Result.Success;

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Non_generic_failure_carries_the_error()
    {
        // Act
        Result result = new TestError(Transient: true);

        // Assert
        Assert.True(result.IsFailure);
        Assert.True(result.Error.IsTransient);
    }

    [Fact]
    public void Error_keeps_the_exception_it_was_converted_from_for_the_log()
    {
        // Arrange
        var cause = new IOException("The process cannot access the file.");

        // Act
        var error = new TestError(Transient: true) { Cause = cause };

        // Assert
        Assert.Same(cause, error.Cause);
        Assert.Contains("The process cannot access the file.", error.ToString());
    }

    [Fact]
    public void Errors_are_logged_with_all_details()
    {
        // Arrange
        Result result = new TestError(Transient: true);

        // Act
        var logged = result.ToString();

        // Assert
        Assert.Equal("Failure: TestError { Code = test.error, IsTransient = True, Cause = , Transient = True }", logged);
    }
}
