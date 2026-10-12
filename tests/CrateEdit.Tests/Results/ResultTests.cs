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

    [Fact]
    public void Value_converts_to_a_successful_result()
    {
        var result = Parse("42");

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Error_converts_to_a_failed_result()
    {
        var result = Parse("forty-two");

        Assert.True(result.IsFailure);
        Assert.Equal("test.error", result.Error.Code);
    }

    [Fact]
    public void Reading_the_value_of_a_failure_is_a_programming_error()
    {
        Assert.Throws<InvalidOperationException>(() => Parse("x").Value);
    }

    [Fact]
    public void Match_selects_the_branch()
    {
        Assert.Equal("42", Parse("42").Match(v => v.ToString(), e => e.Code));
        Assert.Equal("test.error", Parse("x").Match(v => v.ToString(), e => e.Code));
    }

    [Fact]
    public void Non_generic_result_carries_success_or_an_error()
    {
        Result ok = Result.Success;
        Result failed = new TestError(Transient: true);

        Assert.True(ok.IsSuccess);
        Assert.True(failed.IsFailure);
        Assert.True(failed.Error.IsTransient);
    }

    [Fact]
    public void Errors_are_logged_with_all_details()
    {
        Assert.Equal("Failure: TestError { Code = test.error, IsTransient = True, Transient = True }",
            ((Result)new TestError(Transient: true)).ToString());
    }
}
