// Release notes from the tasks closed since the previous release ("Releases" in Development.md).
//
//   dotnet run .github/scripts/release-notes.cs                                   preview the notes of the branch
//   dotnet run .github/scripts/release-notes.cs -- --check --title "<PR title>"   what the PR check does
//
// --title <text>    the PR title; without it, the title of the merged PR is read from the HEAD commit
// --version <ver>   the version of the release; without it, the previous tag plus the bump
// --output <file>   write the notes to the file instead of the console
// --check           PR check: the version bump of the PR title and the commits must be the one the tasks need
//
// Fails (exit code 1) when a closed task has no "## Release note", and with --check when the bump differs.
// The notes are written in any case, so the PR check can show them next to the errors.

using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

Console.OutputEncoding = Encoding.UTF8;

string? title = null, version = null, output = null;
bool check = false;
for (int i = 0; i < args.Length; i++)
{
	string Value() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} needs a value.");
	switch (args[i])
	{
		case "--title": title = Value(); break;
		case "--version": version = Value(); break;
		case "--output": output = Value(); break;
		case "--check": check = true; break;
		default: throw new ArgumentException($"Unknown argument '{args[i]}'.");
	}
}

Environment.CurrentDirectory = Git("rev-parse", "--show-toplevel").Trim();

// HEAD^: on main HEAD is the merge commit, maybe tagged already when the job is re-run; in the PR check it is the
// test merge commit, and HEAD^ is the base branch.
string previous = Git("describe", "--tags", "--abbrev=0", "--match", "[0-9]*.[0-9]*.[0-9]*", "HEAD^").Trim();

var errors = new List<string>();

// Compared with the working tree, so a local preview sees a task moved to done/ and not committed yet.
var tasks = Git("diff", "--no-renames", "--diff-filter=A", "--name-only", previous, "--", "tasks/done/")
	.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
	.Where(path => path.EndsWith(".md", StringComparison.Ordinal))
	.Select(ClosedTask.Read)
	.OrderBy(t => t.Id, StringComparer.Ordinal)
	.ThenBy(t => t.Path, StringComparer.Ordinal)
	.ToList();

foreach (var task in tasks)
{
	if (task.Note is null)
		errors.Add($"{task.Path}: no '## Release note' section. Write the note for the users of the packages, or 'None.' when they see no change.");
	else if (task.Note.Length == 0)
		errors.Add($"{task.Path}: '## Release note' is empty. Write the note, or 'None.' when users see no change.");
	else if (task.Breaking && task.IsNone)
		errors.Add($"{task.Path}: 'breaking: true' with the note 'None.'. Tell the users what to change, or remove 'breaking: true'.");
}

var noted = tasks.Where(t => !string.IsNullOrEmpty(t.Note) && !t.IsNone).ToList();

// The bump the tasks need; no closed task, no requirement: the PR title decides.
Bump? required = null;
string reason = "";
if (tasks.Count > 0)
{
	var breaking = tasks.Where(t => t.Breaking).ToList();
	var features = noted.Where(t => t.Type == "feature").ToList();
	(required, reason) =
		breaking.Count > 0 ? (Bump.Major, $"breaking: {Ids(breaking)}")
		: features.Count > 0 ? (Bump.Minor, $"features: {Ids(features)}")
		: (Bump.Patch, "no breaking change and no feature");
}

// The bump GitVersion gives (.github/GitVersion.yml, Mainline): the largest one over the messages of the merge
// commit, which holds the PR title, and of the merged commits; patch when no message matches.
string gitVersion = File.ReadAllText(".github/GitVersion.yml");
var rules = new (Bump Bump, Regex Regex)[]
{
	(Bump.Major, BumpRegex("major")),
	(Bump.Minor, BumpRegex("minor")),
	(Bump.Patch, BumpRegex("patch")),
};

var messages = new List<(string Source, string Message)>();
if (title is not null)
	messages.Add(("the PR title", title));
foreach (string commit in Git("log", "--format=%h%x1f%B%x1e", $"{previous}..HEAD").Split('\x1e', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
{
	string[] parts = commit.Split('\x1f', 2);
	messages.Add(($"commit {parts[0]}", parts[1]));
}

Bump actual = Bump.Patch;
string actualSource = "no version keyword";
foreach (var (source, message) in messages)
{
	foreach (var rule in rules)
	{
		if (!rule.Regex.IsMatch(message))
			continue;
		if (rule.Bump > actual)
			(actual, actualSource) = (rule.Bump, source);
		break;
	}
}

version ??= Next(previous, actual);

if (check && required is { } need && need != actual)
{
	errors.Add(need > actual
		? $"The tasks need a {Name(need)} version ({reason}), but the PR title and the commits give {Name(actual)}. Start the PR title with '{Prefix(need)}'."
		: $"{char.ToUpperInvariant(actualSource[0])}{actualSource[1..]} gives a {Name(actual)} version, but the tasks need {Name(need)} ({reason}). "
			+ (actualSource.StartsWith("commit", StringComparison.Ordinal)
				? "Reword the commit: commit messages must not contain the version keywords of .github/GitVersion.yml."
				: $"Start the PR title with {(need == Bump.Patch ? "no version keyword" : $"'{Prefix(need)}'")}."));
}

var notes = new StringBuilder();
if (check)
{
	notes.AppendLine($"Merging this pull request releases **{version}**: {previous} + {Name(actual)} ({actualSource}).").AppendLine();
	if (errors.Count > 0)
	{
		notes.AppendLine("**Errors:**").AppendLine();
		foreach (string error in errors)
			notes.AppendLine($"- {error}");
		notes.AppendLine();
	}
	notes.AppendLine("---").AppendLine();
}

var groups = new (string Heading, Func<ClosedTask, bool> Match)[]
{
	("Breaking changes", t => t.Breaking),
	("Features", t => !t.Breaking && t.Type == "feature"),
	("Fixes", t => !t.Breaking && t.Type == "bug"),
	("Other", t => !t.Breaking && t.Type is not ("feature" or "bug")),
};
foreach (var (heading, match) in groups)
{
	var items = noted.Where(match).ToList();
	if (items.Count == 0)
		continue;
	notes.AppendLine($"## {heading}").AppendLine();
	foreach (var task in items)
		notes.Append(Item(task.Note!, task.Issue));
	notes.AppendLine();
}
if (noted.Count == 0)
	notes.Append(Item(title ?? MergedTitle(), null)).AppendLine();

notes.AppendLine($"**Full changelog:** {RepositoryUrl()}/compare/{previous}...{version}");

if (output is null)
	Console.Write(notes);
else
	File.WriteAllText(output, notes.ToString());

foreach (string error in errors)
	Console.Error.WriteLine(Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true" ? $"::error::{error}" : $"error: {error}");
return errors.Count == 0 ? 0 : 1;


Regex BumpRegex(string bump)
{
	var match = Regex.Match(gitVersion, $@"^{bump}-version-bump-message:\s*'(.+)'\s*$", RegexOptions.Multiline);
	if (!match.Success)
		throw new InvalidOperationException($"'{bump}-version-bump-message' not found in .github/GitVersion.yml.");
	// GitVersion compiles the patterns with IgnoreCase.
	return new Regex(match.Groups[1].Value, RegexOptions.IgnoreCase);
}

static string Git(params string[] args)
{
	var start = new ProcessStartInfo("git")
	{
		RedirectStandardOutput = true,
		RedirectStandardError = true,
		StandardOutputEncoding = Encoding.UTF8,
	};
	foreach (string arg in (string[])["-c", "core.quotepath=off", .. args])
		start.ArgumentList.Add(arg);
	using var process = Process.Start(start)!;
	var error = process.StandardError.ReadToEndAsync();
	string result = process.StandardOutput.ReadToEnd();
	process.WaitForExit();
	if (process.ExitCode != 0)
		throw new InvalidOperationException($"git {string.Join(' ', args)} failed: {error.Result.Trim()}");
	return result;
}

// The title of the merged PR: GitHub writes "Merge pull request #N from <branch>", an empty line and the title.
static string MergedTitle()
{
	string[] lines = Git("log", "-1", "--format=%B", "HEAD").Split('\n', StringSplitOptions.TrimEntries);
	return lines[0].StartsWith("Merge pull request #", StringComparison.Ordinal)
		? lines.Skip(1).FirstOrDefault(l => l.Length > 0) ?? lines[0]
		: lines[0];
}

static string RepositoryUrl()
{
	string? server = Environment.GetEnvironmentVariable("GITHUB_SERVER_URL");
	string? repository = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY");
	if (!string.IsNullOrEmpty(server) && !string.IsNullOrEmpty(repository))
		return $"{server}/{repository}";
	string origin = Git("remote", "get-url", "origin").Trim().TrimEnd('/');
	origin = Regex.Replace(origin, @"^git@([^:]+):", "https://$1/");
	return origin.EndsWith(".git", StringComparison.Ordinal) ? origin[..^4] : origin;
}

// A list item: the first paragraph ends with the issue link, the other lines are indented into the item.
static string Item(string note, string? issue)
{
	var lines = note.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
	int blank = lines.FindIndex(l => l.Trim().Length == 0);
	if (issue is not null)
		lines[(blank < 0 ? lines.Count : blank) - 1] += $" ({issue})";
	var item = new StringBuilder();
	for (int i = 0; i < lines.Count; i++)
		item.AppendLine(i == 0 ? $"- {lines[i]}" : lines[i].Length == 0 ? "" : $"  {lines[i]}");
	return item.ToString();
}

static string Next(string previous, Bump bump)
{
	var match = Regex.Match(previous, @"^(\d+)\.(\d+)\.(\d+)$");
	if (!match.Success)
		return "HEAD";
	int major = int.Parse(match.Groups[1].Value), minor = int.Parse(match.Groups[2].Value), patch = int.Parse(match.Groups[3].Value);
	return bump switch
	{
		Bump.Major => $"{major + 1}.0.0",
		Bump.Minor => $"{major}.{minor + 1}.0",
		_ => $"{major}.{minor}.{patch + 1}",
	};
}

static string Ids(IEnumerable<ClosedTask> tasks) => string.Join(", ", tasks.Select(t => t.Id));

static string Name(Bump bump) => bump.ToString().ToLowerInvariant();

static string Prefix(Bump bump) => bump == Bump.Major ? "BREAKING CHANGE: " : "feat: ";

enum Bump { Patch = 1, Minor, Major }

/// <summary>
/// A task file moved to tasks/done/: its front matter and its "## Release note" section
/// (null when the section is missing).
/// </summary>
record ClosedTask(string Path, string Type, bool Breaking, string? Note)
{
	/// <summary>The task number: 0096-13 from 0096-13-item-transaction-settings.md.</summary>
	public string Id => Regex.Match(System.IO.Path.GetFileName(Path), @"^\d{4}(-\d{2})*").Value;

	/// <summary>The GitHub issue of the top-level task: #96 for 0096-13.</summary>
	public string? Issue => Regex.Match(System.IO.Path.GetFileName(Path), @"^\d{4}") is { Success: true } m ? $"#{int.Parse(m.Value)}" : null;

	/// <summary>"None." means the users see no change; a reason may follow.</summary>
	public bool IsNone => Note is not null && Note.StartsWith("None.", StringComparison.Ordinal);

	public static ClosedTask Read(string path)
	{
		string[] lines = File.ReadAllLines(path);

		var frontMatter = new Dictionary<string, string>();
		if (lines.Length > 0 && lines[0].TrimEnd() == "---")
		{
			foreach (string line in lines.Skip(1).TakeWhile(l => l.TrimEnd() != "---"))
			{
				int colon = line.IndexOf(':');
				if (colon > 0)
					frontMatter[line[..colon].Trim()] = line[(colon + 1)..].Trim();
			}
		}

		int start = Array.FindIndex(lines, l => l.TrimEnd() == "## Release note");
		string? note = start < 0
			? null
			: string.Join('\n', lines.Skip(start + 1).TakeWhile(l => !l.StartsWith("## ", StringComparison.Ordinal))).Trim();

		return new ClosedTask(
			path,
			frontMatter.GetValueOrDefault("type", ""),
			frontMatter.GetValueOrDefault("breaking") == "true",
			note);
	}
}
