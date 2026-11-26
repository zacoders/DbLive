using System.Text.RegularExpressions;

enum Bump { Patch = 1, Minor, Major }

/// <summary>A version bump and why: the tasks that need it, or the message that gives it.</summary>
record VersionBump(Bump Bump, string Why)
{
	public string Name => Bump.ToString().ToLowerInvariant();

	/// <summary>The start of the PR title that gives a minor or a major version.</summary>
	public string Prefix => Bump == Bump.Major ? "BREAKING CHANGE: " : "feat: ";

	/// <summary>
	/// The bump the closed tasks need: major for a breaking change, minor for a feature with a note, patch otherwise.
	/// Null without closed tasks: then the PR title decides.
	/// </summary>
	public static VersionBump? NeededBy(List<ClosedTask> tasks)
	{
		if (tasks.Count == 0)
			return null;
		var breaking = tasks.Where(t => t.Breaking).ToList();
		var features = tasks.Where(t => t.IsListed && t.Type == "feature").ToList();
		return breaking.Count > 0 ? new(Bump.Major, $"breaking: {Ids(breaking)}")
			: features.Count > 0 ? new(Bump.Minor, $"features: {Ids(features)}")
			: new(Bump.Patch, "no breaking change and no feature");
	}

	/// <summary>
	/// The bump GitVersion gives (.github/GitVersion.yml, Mainline): the largest one over the messages of the merge
	/// commit, which holds the PR title, and of the merged commits; patch when no message matches.
	/// </summary>
	public static VersionBump GivenBy(string? title, string previous)
	{
		var messages = new List<(string Source, string Message)>();
		if (title is not null)
			messages.Add(("the PR title", title));
		messages.AddRange(Git.CommitsSince(previous).Select(c => ($"commit {c.Hash}", c.Message)));

		var keywords = Keywords();
		var given = new VersionBump(Bump.Patch, "no version keyword");
		foreach (var (source, message) in messages)
		{
			foreach (var (bump, keyword) in keywords)
			{
				if (bump > given.Bump && keyword.IsMatch(message))
					given = new VersionBump(bump, source);
			}
		}
		return given;
	}

	/// <summary>In the PR check, the PR title and the commits must give the bump the tasks need.</summary>
	public static IEnumerable<string> Check(VersionBump? needed, VersionBump given)
	{
		if (needed is null || needed.Bump == given.Bump)
			yield break;

		if (needed.Bump > given.Bump)
			yield return $"The tasks need a {needed.Name} version ({needed.Why}), but the PR title and the commits give {given.Name}. "
				+ $"Start the PR title with '{needed.Prefix}'.";
		else if (given.Why.StartsWith("commit", StringComparison.Ordinal))
			yield return $"{Capitalized(given.Why)} gives a {given.Name} version, but the tasks need {needed.Name} ({needed.Why}). "
				+ "Reword the commit: commit messages must not contain the version keywords of .github/GitVersion.yml.";
		else
			yield return $"{Capitalized(given.Why)} gives a {given.Name} version, but the tasks need {needed.Name} ({needed.Why}). "
				+ $"Start the PR title with {(needed.Bump == Bump.Patch ? "no version keyword" : $"'{needed.Prefix}'")}.";
	}

	/// <summary>The version after the previous release; HEAD when the previous tag is not a version.</summary>
	public string Next(string previous)
	{
		var match = Regex.Match(previous, @"^(\d+)\.(\d+)\.(\d+)$");
		if (!match.Success)
			return "HEAD";
		int major = int.Parse(match.Groups[1].Value), minor = int.Parse(match.Groups[2].Value), patch = int.Parse(match.Groups[3].Value);
		return Bump switch
		{
			Bump.Major => $"{major + 1}.0.0",
			Bump.Minor => $"{major}.{minor + 1}.0",
			_ => $"{major}.{minor}.{patch + 1}",
		};
	}

	// The version keywords from .github/GitVersion.yml, the largest bump first.
	static (Bump Bump, Regex Keyword)[] Keywords()
	{
		string config = File.ReadAllText(".github/GitVersion.yml");
		return [(Bump.Major, Keyword("major")), (Bump.Minor, Keyword("minor")), (Bump.Patch, Keyword("patch"))];

		Regex Keyword(string bump)
		{
			var match = Regex.Match(config, $@"^{bump}-version-bump-message:\s*'(.+)'\s*$", RegexOptions.Multiline);
			if (!match.Success)
				throw new InvalidOperationException($"'{bump}-version-bump-message' not found in .github/GitVersion.yml.");
			// GitVersion compiles the patterns with IgnoreCase.
			return new Regex(match.Groups[1].Value, RegexOptions.IgnoreCase);
		}
	}

	static string Ids(IEnumerable<ClosedTask> tasks) => string.Join(", ", tasks.Select(t => t.Id));

	static string Capitalized(string text) => $"{char.ToUpperInvariant(text[0])}{text[1..]}";
}
