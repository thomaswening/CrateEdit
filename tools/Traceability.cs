// Requirements traceability report and phase exit check.
//
// Lists every requirement from docs/requirements.md with the tests tagged with it
// ([Trait("Req", "REQ-NNN")]) and their results, plus requirements verified manually
// (docs/manual-verification.md). Fails if a test references an unknown requirement.
// With --phase N, also fails unless phases 0 to N of docs/roadmap.md are complete.
//
// What each check means and what to do when it fails:
// https://github.com/thomaswening/CrateEdit/blob/main/docs/development.md#traceability-and-phase-exit-check
//
// Usage:
//   dotnet run tools/Traceability.cs -- --requirements docs/requirements.md
//       --manual docs/manual-verification.md --results <xunit.xml> [--results <xunit.xml> ...]
//       --output <report.md> [--roadmap docs/roadmap.md --phase <N>]
//
// Test results are xUnit XML reports (test assembly run with -xml). With several result files
// (e.g. one per OS), a requirement only counts as passing if its tests pass in all of them.

using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

const int ExitSuccess = 0;
const int ExitFailed = 1;

var options = ParseArguments(args);
var requirementsPath = Single(options, "--requirements");
var requirements = ReadRequirements(requirementsPath);
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
        > 0 when linked.Any(t => t.Result == "Fail") => Status.Failing,
        > 0 when linked.All(t => t.Result == "Pass") => Status.Passing,
        > 0 => Status.Incomplete,
        _ when manual.TryGetValue(requirement.Id, out var verified) => verified ? Status.Manual : Status.ManualNotVerified,
        _ => Status.NotCovered,
    };
    return new Row(requirement, status, names);
}).ToList();

report.AppendLine("| Status | Count |");
report.AppendLine("|---|---|");
foreach (var group in rows.GroupBy(r => r.Status).OrderBy(g => g.Key))
{
    report.AppendLine($"| {Label(group.Key)} | {group.Count()} |");
}

report.AppendLine();
report.AppendLine("| Requirement | Level | Status | Tests |");
report.AppendLine("|---|---|---|---|");
foreach (var (requirement, status, names) in rows)
{
    var testList = string.Join("<br>", names);
    report.AppendLine($"| {requirement.Id} {requirement.Title} | {requirement.Level} | {Label(status)} | {testList} |");
}

if (unknown.Count > 0)
{
    report.AppendLine();
    report.AppendLine($"**Error:** tests reference unknown requirements: {string.Join(", ", unknown)}");
}

var phaseExitFailed = false;
if (options.ContainsKey("--phase"))
{
    var lastPhase = int.Parse(Single(options, "--phase"));
    var phases = ReadPhases(Single(options, "--roadmap"), requirements);
    var resolved = ReadResolvedDecisions(requirementsPath);
    var problems = PhaseExitProblems(phases, lastPhase, rows, resolved);
    AppendPhaseExitCheck(report, lastPhase, problems);
    phaseExitFailed = problems.Count > 0;
}

var output = Single(options, "--output");
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
File.WriteAllText(output, report.ToString());
Console.WriteLine(report);
return unknown.Count > 0 || rows.Any(r => r.Status == Status.Failing) || phaseExitFailed ? ExitFailed : ExitSuccess;

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
        .Select(line => Patterns.RequirementDefinition().Match(line))
        .Where(match => match.Success)
        .Select(match => new Requirement(match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value))
        .OrderBy(r => r.Id, StringComparer.Ordinal)
        .ToList();

/// <summary>Requirement ID to whether its manual check has been done: a "Last verified" cell that starts with a date.</summary>
static Dictionary<string, bool> ReadManualChecks(string path) =>
    File.ReadLines(path)
        .Select(line => Patterns.ManualCheckRow().Match(line))
        .Where(match => match.Success)
        .ToDictionary(
            match => match.Groups[1].Value,
            match => Patterns.VerificationDate().IsMatch(LastCell(match.Value)));

static string LastCell(string tableRow) => tableRow.TrimEnd().TrimEnd('|').Split('|')[^1].Trim();

/// <summary>Open decisions marked "**Resolved**" in their definition.</summary>
static HashSet<string> ReadResolvedDecisions(string requirementsPath) =>
    File.ReadLines(requirementsPath)
        .Select(line => Patterns.ResolvedDecision().Match(line))
        .Where(match => match.Success)
        .Select(match => match.Groups[1].Value)
        .ToHashSet();

/// <summary>
/// The phases of the roadmap: the requirements on each "Requirements:" line, with ranges such as
/// "REQ-123 to REQ-129" expanded to the requirements that exist, and the open decisions on each
/// "Resolves:" and "Needs:" line.
/// </summary>
static List<Phase> ReadPhases(string roadmapPath, List<Requirement> requirements)
{
    var phases = new List<Phase>();
    foreach (var line in File.ReadLines(roadmapPath))
    {
        if (Patterns.PhaseHeading().Match(line) is { Success: true } heading)
        {
            phases.Add(new Phase(int.Parse(heading.Groups[1].Value), [], []));
        }
        else if (line.StartsWith("#", StringComparison.Ordinal))
        {
            // Any other heading ends the current phase, e.g. the next milestone.
            phases.Add(new Phase(-1, [], []));
        }
        else if (phases.Count > 0 && phases[^1].Number >= 0)
        {
            AddPhaseItems(phases[^1], WithoutLinks(line), requirements);
        }
    }

    return phases.Where(phase => phase.Number >= 0).ToList();
}

static void AddPhaseItems(Phase phase, string line, List<Requirement> requirements)
{
    if (line.StartsWith("- Requirements:", StringComparison.Ordinal))
    {
        foreach (Match match in Patterns.RequirementOrRange().Matches(line))
        {
            phase.Requirements.AddRange(ExpandRange(match, requirements));
        }
    }
    else if (line.StartsWith("- Resolves:", StringComparison.Ordinal) || line.StartsWith("- Needs:", StringComparison.Ordinal))
    {
        phase.Decisions.AddRange(Patterns.DecisionId().Matches(line).Select(match => match.Value));
    }
}

/// <summary>"REQ-123" gives itself; "REQ-123 to REQ-129" gives every defined requirement in between.</summary>
static IEnumerable<string> ExpandRange(Match match, List<Requirement> requirements)
{
    var first = match.Groups[1].Value;
    if (!match.Groups[2].Success)
    {
        return [first];
    }

    var last = match.Groups[2].Value;
    return requirements
        .Select(r => r.Id)
        .Where(id => string.CompareOrdinal(id, first) >= 0 && string.CompareOrdinal(id, last) <= 0);
}

/// <summary>"[REQ-088](requirements.md#req-088)" becomes "REQ-088".</summary>
static string WithoutLinks(string line) => Patterns.MarkdownLink().Replace(line, "$1");

/// <summary>
/// Everything that keeps phases 0 to <paramref name="lastPhase"/> from being complete: a
/// requirement that is not passing and not verified by hand, a requirement the roadmap names
/// but requirements.md does not define, and an open decision that is not resolved.
/// </summary>
static List<PhaseProblem> PhaseExitProblems(List<Phase> phases, int lastPhase, List<Row> rows, HashSet<string> resolved)
{
    var problems = new List<PhaseProblem>();
    foreach (var phase in phases.Where(phase => phase.Number <= lastPhase))
    {
        foreach (var id in phase.Requirements.Distinct())
        {
            var row = rows.SingleOrDefault(r => r.Requirement.Id == id);
            if (row is null)
            {
                problems.Add(new PhaseProblem(phase.Number, id, "not defined in requirements.md"));
            }
            else if (row.Status is not (Status.Passing or Status.Manual))
            {
                problems.Add(new PhaseProblem(phase.Number, $"{id} {row.Requirement.Title}", Label(row.Status)));
            }
        }

        foreach (var decision in phase.Decisions.Distinct().Where(decision => !resolved.Contains(decision)))
        {
            problems.Add(new PhaseProblem(phase.Number, decision, "not resolved"));
        }
    }

    return problems;
}

static void AppendPhaseExitCheck(StringBuilder report, int lastPhase, List<PhaseProblem> problems)
{
    report.AppendLine();
    report.AppendLine($"## Phase exit check: phases 0 to {lastPhase}");
    report.AppendLine();
    if (problems.Count == 0)
    {
        report.AppendLine("Complete: every requirement is passing or verified by hand, and every open decision is resolved.");
        return;
    }

    report.AppendLine($"**Error:** {problems.Count} item(s) keep these phases from being complete.");
    report.AppendLine();
    report.AppendLine("| Phase | Item | Problem |");
    report.AppendLine("|---|---|---|");
    foreach (var (phase, item, problem) in problems)
    {
        report.AppendLine($"| {phase} | {item} | {problem} |");
    }
}

static string Label(Status status) => status switch
{
    Status.ManualNotVerified => "Manual, not verified yet",
    Status.NotCovered => "Not covered",
    _ => status.ToString(),
};

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

internal static partial class Patterns
{
    /// <summary>"**REQ-091 — Bounded temporary storage (SHALL).**", optionally after its anchor.</summary>
    [GeneratedRegex(@"^(?:<a id=""[^""]+""></a>)?\*\*(REQ-\d{3}) — (.+?) \(([^)]+)\)\.\*\*")]
    public static partial Regex RequirementDefinition();

    /// <summary>A table row of manual-verification.md whose first cell is a requirement.</summary>
    [GeneratedRegex(@"^\|\s*\[?(REQ-\d{3})\b.*$")]
    public static partial Regex ManualCheckRow();

    /// <summary>"13 Oct 2026" at the start of a "Last verified" cell; "not yet" does not match.</summary>
    [GeneratedRegex(@"^\d{1,2} [A-Z][a-z]+ \d{4}\b")]
    public static partial Regex VerificationDate();

    /// <summary>"**OPEN-C — Safe-write guarantees.** **Resolved**", optionally after its anchor.</summary>
    [GeneratedRegex(@"^(?:<a id=""[^""]+""></a>)?\*\*(OPEN-[A-Z]+) — .+?\*\* \*\*Resolved\*\*")]
    public static partial Regex ResolvedDecision();

    [GeneratedRegex(@"^### Phase (\d+):")]
    public static partial Regex PhaseHeading();

    /// <summary>"REQ-108" or "REQ-123 to REQ-129".</summary>
    [GeneratedRegex(@"(REQ-\d{3})(?: to (REQ-\d{3}))?")]
    public static partial Regex RequirementOrRange();

    [GeneratedRegex(@"OPEN-[A-Z]+")]
    public static partial Regex DecisionId();

    /// <summary>A Markdown link whose text is a requirement or decision ID.</summary>
    [GeneratedRegex(@"\[((?:REQ-\d{3}|OPEN-[A-Z]+))\]\([^)]*\)")]
    public static partial Regex MarkdownLink();
}

/// <summary>Order is the order of the summary table.</summary>
internal enum Status
{
    Failing,
    Incomplete,
    Manual,
    ManualNotVerified,
    NotCovered,
    Passing,
}

internal sealed record Requirement(string Id, string Title, string Level);

internal sealed record TestResult(string Name, string Result, HashSet<string> Requirements);

internal sealed record Row(Requirement Requirement, Status Status, List<string> Names);

internal sealed record Phase(int Number, List<string> Requirements, List<string> Decisions);

internal sealed record PhaseProblem(int Phase, string Item, string Problem);
