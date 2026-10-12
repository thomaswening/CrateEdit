// Requirement links in the documentation.
//
// Every requirement (REQ-NNN) and open decision (OPEN-X) in docs/requirements.md gets an anchor,
// and every mention in the Markdown docs links to it, so readers can jump to the definition.
//
// Usage:
//   dotnet run tools/RequirementLinks.cs -- --check   fails on unlinked mentions and broken links
//   dotnet run tools/RequirementLinks.cs -- --fix     adds missing anchors and links in place
//
// Mentions inside code (`...` or fenced blocks) and inside existing links are left alone.

using System.Text;
using System.Text.RegularExpressions;

var fix = args.Contains("--fix");
if (!fix && !args.Contains("--check"))
{
    Console.Error.WriteLine("Specify --check or --fix.");
    return 2;
}

var root = FindRepositoryRoot();
var requirementsPath = Path.Combine(root, "docs", "requirements.md");
var markdownFiles = Directory.EnumerateFiles(Path.Combine(root, "docs"), "*.md", SearchOption.AllDirectories)
    .Append(Path.Combine(root, "README.md"))
    .Where(File.Exists)
    .Order(StringComparer.Ordinal)
    .ToList();

var definition = new Regex(@"^(?<anchor><a id=""(?<anchorId>[a-z0-9-]+)""></a>)?\*\*(?<id>REQ-\d{3}|OPEN-[A-Z]) — ");
var defined = File.ReadLines(requirementsPath)
    .Select(line => definition.Match(line))
    .Where(match => match.Success)
    .Select(match => match.Groups["id"].Value)
    .ToHashSet();

// Matches, in order of precedence: inline code, an existing Markdown link, a link target to an
// anchor in requirements.md, or a bare mention.
var token = new Regex(
    @"(?<code>`[^`]*`)|(?<link>\[[^\]]*\]\((?<target>[^)]*)\))|\b(?<mention>REQ-\d{3}|OPEN-[A-Z])\b");
var anchorTarget = new Regex(@"(?:^|/)requirements\.md#(?<anchor>[a-z0-9-]+)$|^#(?<anchor>[a-z0-9-]+)$");

var problems = new List<string>();
foreach (var file in markdownFiles)
{
    var isRequirements = Path.GetFullPath(file) == Path.GetFullPath(requirementsPath);
    var relativeToRequirements = isRequirements
        ? ""
        : Path.GetRelativePath(Path.GetDirectoryName(file)!, requirementsPath).Replace('\\', '/');
    var lines = File.ReadAllLines(file);
    var inFence = false;
    var changed = false;

    for (var i = 0; i < lines.Length; i++)
    {
        var line = lines[i];
        if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
        {
            inFence = !inFence;
            continue;
        }

        if (inFence)
        {
            continue;
        }

        // The defining line of a requirement: ensure its anchor, and do not link the definition itself.
        var prefix = "";
        var rest = line;
        if (isRequirements && definition.Match(line) is { Success: true } def)
        {
            var id = def.Groups["id"].Value;
            var expectedAnchor = $"<a id=\"{AnchorFor(id)}\"></a>";
            if (def.Groups["anchor"].Value != expectedAnchor)
            {
                problems.Add($"{Describe(file, i)}: missing anchor for {id}");
            }

            prefix = expectedAnchor + $"**{id} — ";
            rest = line[(def.Length)..];
        }

        var linked = token.Replace(rest, match =>
        {
            if (match.Groups["link"].Success)
            {
                var target = match.Groups["target"].Value;
                if (anchorTarget.Match(target) is { Success: true } a && IdFor(a.Groups["anchor"].Value) is { } linkedId &&
                    (target.StartsWith('#') ? isRequirements : true) && !defined.Contains(linkedId))
                {
                    problems.Add($"{Describe(file, i)}: link to unknown {linkedId}");
                }

                return match.Value;
            }

            if (match.Groups["mention"].Success)
            {
                var id = match.Groups["mention"].Value;
                if (!defined.Contains(id))
                {
                    problems.Add($"{Describe(file, i)}: {id} is not defined in docs/requirements.md");
                    return match.Value;
                }

                problems.Add($"{Describe(file, i)}: {id} is not linked");
                return $"[{id}]({relativeToRequirements}#{AnchorFor(id)})";
            }

            return match.Value;
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

if (fix)
{
    var fixable = problems.Count(p => p.EndsWith("is not linked", StringComparison.Ordinal) || p.Contains("missing anchor"));
    Console.WriteLine($"Fixed {fixable} problem(s).");
    foreach (var problem in problems.Where(p => !p.EndsWith("is not linked", StringComparison.Ordinal) && !p.Contains("missing anchor")))
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
    ? "All requirement mentions are linked."
    : $"{problems.Count} problem(s). Run: dotnet run tools/RequirementLinks.cs -- --fix");
return problems.Count == 0 ? 0 : 1;

string Describe(string file, int lineIndex) => $"{Path.GetRelativePath(root, file).Replace('\\', '/')}:{lineIndex + 1}";

static string AnchorFor(string id) => id.ToLowerInvariant();

static string? IdFor(string anchor) =>
    Regex.IsMatch(anchor, @"^(req-\d{3}|open-[a-z])$") ? anchor.ToUpperInvariant() : null;

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
