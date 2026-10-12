// Links to requirements and decisions in the documentation and the code.
//
// Every requirement (REQ-NNN) and open decision (OPEN-X) in docs/requirements.md gets an anchor.
// Every mention of one, and of an architecture decision record (ADR-NNNN), links to it:
// - in Markdown files (docs/, README.md) with relative links;
// - in XML documentation comments (/// lines in src/ and tests/) with <see href="..."> links to
//   the file on GitHub, so they can be followed from the IDE.
//
// Usage:
//   dotnet run tools/RequirementLinks.cs -- --check   fails on unlinked mentions and broken links
//   dotnet run tools/RequirementLinks.cs -- --fix     adds missing anchors and links in place
//
// Mentions inside code (`...`, <c>...</c>, fenced blocks), inside existing links, and in Markdown
// headings are left alone.

using System.Text;
using System.Text.RegularExpressions;

const string GitHubBase = "https://github.com/thomaswening/CrateEdit/blob/main/";

var fix = args.Contains("--fix");
if (!fix && !args.Contains("--check"))
{
    Console.Error.WriteLine("Specify --check or --fix.");
    return 2;
}

var root = FindRepositoryRoot();
var requirementsPath = Path.Combine(root, "docs", "requirements.md");
var adrDirectory = Path.Combine(root, "docs", "adr");

var definition = new Regex(@"^(?<anchor><a id=""(?<anchorId>[a-z0-9-]+)""></a>)?\*\*(?<id>REQ-\d{3}|OPEN-[A-Z]) — ");
var requirements = File.ReadLines(requirementsPath)
    .Select(line => definition.Match(line))
    .Where(match => match.Success)
    .Select(match => match.Groups["id"].Value)
    .ToHashSet();
var adrs = Directory.EnumerateFiles(adrDirectory, "*.md")
    .Select(Path.GetFileName)
    .Select(name => Regex.Match(name!, @"^(?<number>\d{4})-.+\.md$"))
    .Where(match => match.Success)
    .ToDictionary(match => "ADR-" + match.Groups["number"].Value, match => match.Value);

const string Mention = @"\b(?<mention>REQ-\d{3}|OPEN-[A-Z]|ADR-\d{4})\b";
var markdownToken = new Regex(@"(?<code>`[^`]*`)|(?<link>\[[^\]]*\]\((?<target>[^)]*)\))|" + Mention);
var xmlDocToken = new Regex(@"(?<code><c>.*?</c>)|(?<link><see\b[^>]*?(?:/>|>.*?</see>))|(?<code>`[^`]*`)|" + Mention);
var requirementTarget = new Regex(@"requirements\.md#(?<anchor>[a-z0-9-]+)(?:""|$)|^#(?<anchor>[a-z0-9-]+)$");
var adrTarget = new Regex(@"adr/(?<file>\d{4}-[^/""#)]+\.md)|^(?<file>\d{4}-[^/""#)]+\.md)$");

var problems = new List<string>();

var markdownFiles = Directory.EnumerateFiles(Path.Combine(root, "docs"), "*.md", SearchOption.AllDirectories)
    .Append(Path.Combine(root, "README.md"))
    .Where(File.Exists);
foreach (var file in markdownFiles.Order(StringComparer.Ordinal))
{
    ProcessFile(file, isMarkdown: true);
}

var codeFiles = new[] { "src", "tests" }
    .SelectMany(folder => Directory.EnumerateFiles(Path.Combine(root, folder), "*.cs", SearchOption.AllDirectories))
    .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                   !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));
foreach (var file in codeFiles.Order(StringComparer.Ordinal))
{
    ProcessFile(file, isMarkdown: false);
}

var fixable = problems.Count(IsFixable);
if (fix)
{
    Console.WriteLine($"Fixed {fixable} problem(s).");
    foreach (var problem in problems.Where(p => !IsFixable(p)))
    {
        Console.WriteLine(problem);
    }

    return problems.Count == fixable ? 0 : 1;
}

foreach (var problem in problems)
{
    Console.WriteLine(problem);
}

Console.WriteLine(problems.Count == 0
    ? "All requirement and decision mentions are linked."
    : $"{problems.Count} problem(s). Run: dotnet run tools/RequirementLinks.cs -- --fix");
return problems.Count == 0 ? 0 : 1;

void ProcessFile(string file, bool isMarkdown)
{
    var isRequirements = Path.GetFullPath(file) == Path.GetFullPath(requirementsPath);
    var directory = Path.GetDirectoryName(file)!;
    var lines = File.ReadAllLines(file);
    var inFence = false;
    var changed = false;

    for (var i = 0; i < lines.Length; i++)
    {
        var line = lines[i];
        string prefix;
        string rest;

        if (isMarkdown)
        {
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                inFence = !inFence;
                continue;
            }

            if (inFence || line.StartsWith('#'))
            {
                continue;
            }

            prefix = "";
            rest = line;

            // The defining line of a requirement: ensure its anchor, and do not link the definition itself.
            if (isRequirements && definition.Match(line) is { Success: true } def)
            {
                var id = def.Groups["id"].Value;
                var expectedAnchor = $"<a id=\"{id.ToLowerInvariant()}\"></a>";
                if (def.Groups["anchor"].Value != expectedAnchor)
                {
                    problems.Add($"{Describe(file, i)}: missing anchor for {id}");
                }

                prefix = expectedAnchor + $"**{id} — ";
                rest = line[def.Length..];
            }
        }
        else
        {
            var docStart = line.IndexOf("///", StringComparison.Ordinal);
            if (docStart < 0 || line[..docStart].Trim().Length > 0)
            {
                continue;
            }

            prefix = line[..(docStart + 3)];
            rest = line[(docStart + 3)..];
        }

        var token = isMarkdown ? markdownToken : xmlDocToken;
        var linked = token.Replace(rest, match =>
        {
            if (match.Groups["link"].Success)
            {
                CheckLink(file, i, match.Value, match.Groups["target"].Success ? match.Groups["target"].Value : match.Value, isRequirements);
                return match.Value;
            }

            if (!match.Groups["mention"].Success)
            {
                return match.Value;
            }

            var id = match.Groups["mention"].Value;
            var target = TargetFor(id, directory, isMarkdown, isRequirements);
            if (target is null)
            {
                problems.Add($"{Describe(file, i)}: {id} does not exist");
                return match.Value;
            }

            problems.Add($"{Describe(file, i)}: {id} is not linked");
            return isMarkdown ? $"[{id}]({target})" : $"<see href=\"{target}\">{id}</see>";
        });

        var updated = prefix + linked;
        if (updated != line)
        {
            lines[i] = updated;
            changed = true;
        }
    }

    if (fix && changed)
    {
        File.WriteAllText(file, string.Join('\n', lines) + "\n", new UTF8Encoding(false));
    }
}

string? TargetFor(string id, string fromDirectory, bool isMarkdown, bool isRequirements)
{
    if (id.StartsWith("ADR-", StringComparison.Ordinal))
    {
        if (!adrs.TryGetValue(id, out var adrFile))
        {
            return null;
        }

        return isMarkdown
            ? Path.GetRelativePath(fromDirectory, Path.Combine(adrDirectory, adrFile)).Replace('\\', '/')
            : GitHubBase + "docs/adr/" + adrFile;
    }

    if (!requirements.Contains(id))
    {
        return null;
    }

    var anchor = "#" + id.ToLowerInvariant();
    if (!isMarkdown)
    {
        return GitHubBase + "docs/requirements.md" + anchor;
    }

    return isRequirements ? anchor : Path.GetRelativePath(fromDirectory, requirementsPath).Replace('\\', '/') + anchor;
}

void CheckLink(string file, int lineIndex, string link, string target, bool isRequirements)
{
    if (requirementTarget.Match(target) is { Success: true } requirement)
    {
        var id = requirement.Groups["anchor"].Value.ToUpperInvariant();
        var isOwnAnchor = target.StartsWith('#');
        if (Regex.IsMatch(id, @"^(REQ-\d{3}|OPEN-[A-Z])$") && (!isOwnAnchor || isRequirements) && !requirements.Contains(id))
        {
            problems.Add($"{Describe(file, lineIndex)}: link to unknown {id}");
        }
    }

    if (adrTarget.Match(target) is { Success: true } adr && !adrs.ContainsValue(adr.Groups["file"].Value))
    {
        problems.Add($"{Describe(file, lineIndex)}: link to unknown decision record {adr.Groups["file"].Value}");
    }
}

bool IsFixable(string problem) =>
    problem.EndsWith("is not linked", StringComparison.Ordinal) || problem.Contains("missing anchor", StringComparison.Ordinal);

string Describe(string file, int lineIndex) => $"{Path.GetRelativePath(root, file).Replace('\\', '/')}:{lineIndex + 1}";

static string FindRepositoryRoot()
{
    for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
    {
        if (File.Exists(Path.Combine(dir.FullName, "CrateEdit.slnx")))
        {
            return dir.FullName;
        }
    }

    throw new InvalidOperationException("Run this tool inside the CrateEdit repository.");
}
