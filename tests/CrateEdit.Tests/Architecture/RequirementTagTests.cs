using System.Reflection;
using System.Text.RegularExpressions;

namespace CrateEdit.Tests.Architecture;

/// <summary>
/// Requirement tags on tests feed the traceability report, so a mistyped or outdated ID would
/// silently claim coverage for nothing. Every tag must name a requirement that exists.
/// </summary>
public partial class RequirementTagTests
{
    [Fact]
    public void Every_requirement_tag_names_an_existing_requirement()
    {
        var known = File.ReadLines(Path.Combine(RepositoryFiles.Root, "docs", "requirements.md"))
            .Select(line => RequirementHeading().Match(line))
            .Where(match => match.Success)
            .Select(match => match.Groups[1].Value)
            .ToHashSet();

        var unknown =
            from type in typeof(RequirementTagTests).Assembly.GetTypes()
            from member in new MemberInfo[] { type }.Concat(type.GetMethods())
            from attribute in member.GetCustomAttributesData()
            where attribute.AttributeType == typeof(TraitAttribute)
                  && (string?)attribute.ConstructorArguments[0].Value == "Req"
            let id = (string)attribute.ConstructorArguments[1].Value!
            where !known.Contains(id)
            select $"{type.Name}.{member.Name}: {id}";

        Assert.NotEmpty(known);
        Assert.Empty(unknown);
    }

    [GeneratedRegex(@"^\*\*(REQ-\d{3}) — ")]
    private static partial Regex RequirementHeading();
}
