using System.Text;

/// <summary>The parts of the release notes, in Markdown.</summary>
static class ReleaseNotes
{
	static readonly (string Heading, Func<ClosedTask, bool> Match)[] Groups =
	[
		("Breaking changes", t => t.Breaking),
		("Features", t => !t.Breaking && t.Type == "feature"),
		("Fixes", t => !t.Breaking && t.Type == "bug"),
		("Other", t => !t.Breaking && t.Type is not ("feature" or "bug")),
	];

	/// <summary>For the PR check: the version that merging releases, and the errors.</summary>
	public static string CheckSummary(string previous, string version, VersionBump given, List<string> errors)
	{
		var text = new StringBuilder();
		text.AppendLine($"Merging this pull request releases **{version}**: {previous} + {given.Name} ({given.Why}).").AppendLine();
		if (errors.Count > 0)
		{
			text.AppendLine("**Errors:**").AppendLine();
			foreach (string error in errors)
				text.AppendLine($"- {error}");
			text.AppendLine();
		}
		text.AppendLine("---").AppendLine();
		return text.ToString();
	}

	/// <summary>
	/// The notes of the tasks under the headings of <see cref="Groups"/>. Without any, the PR title: a small fix
	/// without a task.
	/// </summary>
	public static string Changes(List<ClosedTask> tasks, string? title)
	{
		var listed = tasks.Where(t => t.IsListed).ToList();
		var text = new StringBuilder();
		foreach (var (heading, match) in Groups)
		{
			var items = listed.Where(match).ToList();
			if (items.Count == 0)
				continue;
			text.AppendLine($"## {heading}").AppendLine();
			foreach (var task in items)
				text.Append(Item(task.Note!, task.Issue));
			text.AppendLine();
		}
		if (listed.Count == 0)
			text.Append(Item(title ?? Git.MergedTitle(), null)).AppendLine();
		return text.ToString();
	}

	public static string ChangelogLink(string previous, string version)
		=> $"**Full changelog:** {Git.RepositoryUrl()}/compare/{previous}...{version}{Environment.NewLine}";

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
}
