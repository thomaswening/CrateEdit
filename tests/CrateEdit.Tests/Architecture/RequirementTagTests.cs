using System.Reflection;
using System.Text.RegularExpressions;

namespace CrateEdit.Tests.Architecture;

/// <summary>
/// Requirement tags on tests feed the traceability report, so a mistyped or outdated ID would
/// silently claim coverage for nothing. Every tag must name a requirement that exists.
/// </summary>
public partial class RequirementTagTests
{
    private static HashSet<string> RequirementIds() =>
        File.ReadLines(Path.Combine(RepositoryFiles.Root, "docs", "requirements.md"))
            .Select(line => RequirementHeading().Match(line))
            .Where(match => match.Success)
            .Select(match => match.Groups[1].Value)
            .ToHashSet();

    /// <summary>Every requirement tag on a test class or method, with where it is ("Type.Member").</summary>
    private static IEnumerable<(string Location, string Id)> RequirementTags() =>
        from type in typeof(RequirementTagTests).Assembly.GetTypes()
        from member in new MemberInfo[] { type }.Concat(type.GetMethods())
        from attribute in member.GetCustomAttributesData()
        where attribute.AttributeType == typeof(TraitAttribute)
              && (string?)attribute.ConstructorArguments[0].Value == "Req"
        select ($"{type.Name}.{member.Name}", (string)attribute.ConstructorArguments[1].Value!);

    [Fact]
    public void Every_requirement_tag_names_an_existing_requirement()
    {
        // Arrange
        var known = RequirementIds();
        Assert.NotEmpty(known);

        // Act
        var unknown = RequirementTags().Where(tag => !known.Contains(tag.Id));

        // Assert
        Assert.Empty(unknown);
    }

    [GeneratedRegex(@"^(?:<a id=""[^""]+""></a>)?\*\*(REQ-\d{3}) — ")]
    private static partial Regex RequirementHeading();
}
