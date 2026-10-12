// Requirements traceability report.
//
// Lists every requirement from docs/requirements.md with the tests tagged with it
// ([Trait("Req", "REQ-NNN")]) and their results, plus requirements verified manually
// (docs/manual-verification.md). Fails if a test references an unknown requirement.
//
// Usage:
//   dotnet run tools/Traceability.cs -- --requirements docs/requirements.md
//       --manual docs/manual-verification.md --results <xunit.xml> [--results <xunit.xml> ...]
//       --output <report.md>
//
// Test results are xUnit XML reports (test assembly run with -xml). With several result files
// (e.g. one per OS), a requirement only counts as passing if its tests pass in all of them.

using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

var options = ParseArguments(args);
var requirements = ReadRequirements(Single(options, "--requirements"));
var manual = ReadManualChecks(Single(options, "--manual"));
var resultFiles = options.GetValueOrDefault("--results") ?? [];
var tests = resultFiles.SelectMany(ReadTests).ToList();

var unknown = tests.SelectMany(t => t.Requirements).Distinct()
    .Where(id => !requirements.Any(r => r.Id == id)).Order().ToList();

var report = new StringBuilder();
report.AppendLine("# Requirements traceability");
report.AppendLine();
report.AppendLine($"Results from: {string.Join(", ", resultFiles.Select(Path.GetFileName))}");
report.AppendLine();

var rows = requirements.Select(requirement =>
{
    // Skipped tests (e.g. tests that need local sample files) neither cover nor fail a requirement.
    var linked = tests.Where(t => t.Requirements.Contains(requirement.Id) && t.Result != "Skip").ToList();
    var names = linked.Select(t => t.Name).Distinct()
        .GroupBy(ShortName)
        .Select(cases => cases.Count() > 1 ? $"{cases.Key} ({cases.Count()} cases)" : cases.Key)
        .ToList();
    var status = linked.Count switch
    {
        > 0 when linked.Any(t => t.Result == "Fail") => "Failing",
        > 0 when linked.All(t => t.Result == "Pass") => "Passing",
        > 0 => "Incomplete",
        _ when manual.Contains(requirement.Id) => "Manual",
        _ => "Not covered",
    };
    return (requirement, status, names);
}).ToList();

report.AppendLine("| Status | Count |");
report.AppendLine("|---|---|");
foreach (var group in rows.GroupBy(r => r.status).OrderBy(g => g.Key))
{
    report.AppendLine($"| {group.Key} | {group.Count()} |");
}

report.AppendLine();
report.AppendLine("| Requirement | Level | Status | Tests |");
report.AppendLine("|---|---|---|---|");
foreach (var (requirement, status, names) in rows)
{
    var testList = string.Join("<br>", names);
    report.AppendLine($"| {requirement.Id} {requirement.Title} | {requirement.Level} | {status} | {testList} |");
}

if (unknown.Count > 0)
{
    report.AppendLine();
    report.AppendLine($"**Error:** tests reference unknown requirements: {string.Join(", ", unknown)}");
}

var output = Single(options, "--output");
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
File.WriteAllText(output, report.ToString());
Console.WriteLine(report);
return unknown.Count > 0 || rows.Any(r => r.status == "Failing") ? 1 : 0;

static Dictionary<string, List<string>> ParseArguments(string[] args)
{
    var result = new Dictionary<string, List<string>>();
    for (var i = 0; i + 1 < args.Length; i += 2)
    {
        if (!result.TryGetValue(args[i], out var values))
        {
            result[args[i]] = values = [];
        }

        values.Add(args[i + 1]);
    }

    return result;
}

static string Single(Dictionary<string, List<string>> options, string name) =>
    options.TryGetValue(name, out var values) && values.Count == 1
        ? values[0]
        : throw new ArgumentException($"Expected exactly one {name} argument.");

static List<Requirement> ReadRequirements(string path) =>
    File.ReadLines(path)
        .Select(line => Regex.Match(line, @"^\*\*(REQ-\d{3}) — (.+?) \(([^)]+)\)\.\*\*"))
        .Where(match => match.Success)
        .Select(match => new Requirement(match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value))
        .OrderBy(r => r.Id, StringComparer.Ordinal)
        .ToList();

static HashSet<string> ReadManualChecks(string path) =>
    File.ReadLines(path)
        .Select(line => Regex.Match(line, @"^\|\s*(REQ-\d{3})\s*\|"))
        .Where(match => match.Success)
        .Select(match => match.Groups[1].Value)
        .ToHashSet();

static IEnumerable<TestResult> ReadTests(string path) =>
    XDocument.Load(path).Descendants("test").Select(test => new TestResult(
        (string)test.Attribute("name")!,
        (string)test.Attribute("result")!,
        test.Element("traits")?.Elements("trait")
            .Where(trait => (string?)trait.Attribute("name") == "Req")
            .Select(trait => (string)trait.Attribute("value")!)
            .ToHashSet() ?? []));

static string ShortName(string fullName)
{
    // "Namespace.Class.Method(args)" -> "Class.Method"
    var nameEnd = fullName.IndexOf('(') is var paren and >= 0 ? paren : fullName.Length;
    var classStart = fullName.LastIndexOf('.', fullName.LastIndexOf('.', nameEnd - 1) - 1) + 1;
    return fullName[classStart..nameEnd];
}

internal sealed record Requirement(string Id, string Title, string Level);

internal sealed record TestResult(string Name, string Result, HashSet<string> Requirements);
