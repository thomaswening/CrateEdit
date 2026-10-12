using CrateEdit.Core;
using NetArchTest.Rules;

namespace CrateEdit.Tests.Architecture;

/// <summary>
/// Guards the single write path (<see href="https://github.com/thomaswening/CrateEdit/blob/main/docs/adr/0003-staging-first.md">ADR-0003</see>, <see href="https://github.com/thomaswening/CrateEdit/blob/main/docs/requirements.md#req-052">REQ-052</see>, <see href="https://github.com/thomaswening/CrateEdit/blob/main/docs/requirements.md#req-075">REQ-075</see>): audio files may only be written by
/// the apply pipeline. No other code, including the UI, may reference the tag writer.
/// </summary>
[Trait("Req", "REQ-052")]
[Trait("Req", "REQ-075")]
public class WritePathTests
{
    private const string WriterNamespace = "CrateEdit.Core.Tags.Writing";
    private const string ApplyNamespace = "CrateEdit.Core.Apply";

    [Fact]
    public void Only_the_apply_pipeline_references_the_tag_writer_in_core()
    {
        var result = Types.InAssembly(typeof(AppInfo).Assembly)
            .That().DoNotResideInNamespace(WriterNamespace)
            .And().DoNotResideInNamespace(ApplyNamespace)
            .ShouldNot().HaveDependencyOn(WriterNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void The_ui_never_references_the_tag_writer()
    {
        var result = Types.InAssembly(typeof(App.App).Assembly)
            .ShouldNot().HaveDependencyOn(WriterNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    private static string Describe(NetArchTest.Rules.TestResult result) =>
        "Types referencing the tag writer: " + string.Join(", ", result.FailingTypeNames ?? []);
}
