using System.Text.RegularExpressions;

/// <summary>
/// MinCompatibleVersion, the minimum DbLive version a database needs ("Compatibility of the dblive tables" in
/// Development.md), is the version of the release that ships the incompatible change.
/// </summary>
static class Compatibility
{
	const string SourceFile = "src/DbLive/SelfDeployer/DbLiveCompatibility.cs";
	const string Declaration = "Version MinCompatibleVersion = ";

	/// <summary>
	/// When MinCompatibleVersion changed since the previous release, it must be the version of this release.
	/// Checked when that version is known: in the PR check, with the PR title, and on main.
	/// </summary>
	/// <remarks>
	/// Any commit since the previous release that changed it counts, not only the difference to the release: two pull
	/// requests that raised it to the same version at the same time merge without a conflict, and the second one is
	/// released with a later version.
	/// </remarks>
	public static IEnumerable<string> Check(string previous, string version)
	{
		string? current = MinCompatibleVersion(File.Exists(SourceFile) ? File.ReadAllText(SourceFile) : null);
		string? before = MinCompatibleVersion(Git.RunOrNull("show", $"{previous}:{SourceFile}"));
		// --full-history: without it git drops the branch of the second pull request, whose merge leaves the file as it was.
		string[] commits = Git.Run("log", "--full-history", "--format=%h", "-G", Declaration, $"{previous}..HEAD", "--", SourceFile)
			.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

		if (current is null)
			yield return $"{SourceFile}: '{Declaration}new(<major>, <minor>, <patch>);' not found.";
		else if ((current != before || commits.Length > 0) && current != version)
			yield return $"MinCompatibleVersion in {SourceFile} changed since {previous} "
				+ $"({before ?? "none"} -> {current}{(commits.Length > 0 ? $", commits {string.Join(", ", commits)}" : "")}), "
				+ $"so it must be the version of this release: set it to {version}. A database deployed by this release then needs DbLive {version} or newer.";
	}

	// The value in the source of DbLiveCompatibility.cs, as major.minor.patch; null without the file or the declaration.
	static string? MinCompatibleVersion(string? source)
		=> source is not null
			&& Regex.Match(source, @"Version MinCompatibleVersion = new\((\d+), (\d+), (\d+)\);") is { Success: true } match
			? $"{match.Groups[1].Value}.{match.Groups[2].Value}.{match.Groups[3].Value}"
			: null;
}
