using System.Text.RegularExpressions;

/// <summary>
/// The public API of the packages ("The public API" in Development.md): PublicAPI.Shipped.txt is the API on main, so a
/// pull request moves its PublicAPI.Unshipped.txt there, and a symbol that leaves it is a breaking change.
/// </summary>
static class PublicApi
{
	const string MoveCommand = "dotnet run .github/scripts/public-api.cs";

	/// <summary>
	/// Checked when the version of the release is known: in the PR check, with the PR title, and on main.
	/// Every PublicAPI.Unshipped.txt is empty, and a symbol removed from a PublicAPI.Shipped.txt since the previous
	/// release, or changed, needs a major version.
	/// </summary>
	public static IEnumerable<string> Check(string previous, string version)
	{
		foreach (string package in ApiFile.Packages())
		{
			string unshippedPath = $"{package}/{ApiFile.Unshipped}";
			int unshipped = ApiFile.Symbols(File.Exists(unshippedPath) ? File.ReadAllText(unshippedPath) : null).Count;
			if (unshipped > 0)
				yield return $"{unshippedPath} has {unshipped} line(s): move them to {ApiFile.Shipped} with '{MoveCommand}'.";
		}

		if (Major(version) > Major(previous))
			yield break;

		// the packages of the previous release too: one without its file now has removed all its symbols.
		IEnumerable<string> shippedPaths = ApiFile.Packages()
			.Select(package => $"{package}/{ApiFile.Shipped}")
			.Union(Git.Run("ls-tree", "-r", "--name-only", previous, "--", "src")
				.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				.Where(path => path.EndsWith($"/{ApiFile.Shipped}", StringComparison.Ordinal)), StringComparer.Ordinal);
		foreach (string path in shippedPaths)
		{
			List<string> before = ApiFile.Symbols(Git.RunOrNull("show", $"{previous}:{path}"));
			var now = ApiFile.Symbols(File.Exists(path) ? File.ReadAllText(path) : null).ToHashSet(StringComparer.Ordinal);
			List<string> removed = before.Where(symbol => !now.Contains(symbol)).ToList();
			if (removed.Count > 0)
				yield return $"{path}: {removed.Count} public symbol(s) removed or changed since {previous}, for example '{removed[0]}'. "
					+ $"That is a breaking change, so the release must be a major version, not {version}: start the PR title with 'BREAKING CHANGE: '.";
		}
	}

	// the major part of a version; -1 when it is not a version (HEAD).
	static int Major(string version)
		=> Regex.Match(version, @"^(\d+)\.") is { Success: true } match ? int.Parse(match.Groups[1].Value) : -1;
}
