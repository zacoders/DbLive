using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>What release-notes.cs asks git.</summary>
static class Git
{
	/// <summary>The tag of the previous release.</summary>
	public static string PreviousRelease()
		// HEAD^: on main HEAD is the merge commit, maybe tagged already when the job is re-run; in the PR check it is
		// the test merge commit, and HEAD^ is the base branch.
		=> Run("describe", "--tags", "--abbrev=0", "--match", "[0-9]*.[0-9]*.[0-9]*", "HEAD^").Trim();

	/// <summary>The commits since the previous release: the short hash and the message.</summary>
	public static IEnumerable<(string Hash, string Message)> CommitsSince(string previous)
		=> Run("log", "--format=%h%x1f%B%x1e", $"{previous}..HEAD")
			.Split('\x1e', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Select(commit => commit.Split('\x1f', 2))
			.Select(parts => (parts[0], parts[1]));

	/// <summary>The title of the merged PR: GitHub writes "Merge pull request #N from &lt;branch&gt;", an empty line and the title.</summary>
	public static string MergedTitle()
	{
		string[] lines = Run("log", "-1", "--format=%B", "HEAD").Split('\n', StringSplitOptions.TrimEntries);
		return lines[0].StartsWith("Merge pull request #", StringComparison.Ordinal)
			? lines.Skip(1).FirstOrDefault(l => l.Length > 0) ?? lines[0]
			: lines[0];
	}

	/// <summary>The web address of the repository, for the changelog link.</summary>
	public static string RepositoryUrl()
	{
		string? server = Environment.GetEnvironmentVariable("GITHUB_SERVER_URL");
		string? repository = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY");
		if (!string.IsNullOrEmpty(server) && !string.IsNullOrEmpty(repository))
			return $"{server}/{repository}";
		string origin = Run("remote", "get-url", "origin").Trim().TrimEnd('/');
		origin = Regex.Replace(origin, @"^git@([^:]+):", "https://$1/");
		return origin.EndsWith(".git", StringComparison.Ordinal) ? origin[..^4] : origin;
	}

	/// <summary>The output of git; throws when git fails.</summary>
	public static string Run(params string[] args)
	{
		var (exitCode, output, error) = Start(args);
		if (exitCode != 0)
			throw new InvalidOperationException($"git {string.Join(' ', args)} failed: {error.Trim()}");
		return output;
	}

	/// <summary>The output of git; null when git fails: a file that is not in the commit.</summary>
	public static string? RunOrNull(params string[] args)
	{
		var (exitCode, output, _) = Start(args);
		return exitCode == 0 ? output : null;
	}

	static (int ExitCode, string Output, string Error) Start(string[] args)
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
		string output = process.StandardOutput.ReadToEnd();
		process.WaitForExit();
		return (process.ExitCode, output, error.Result);
	}
}
