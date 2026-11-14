using System.Text.RegularExpressions;

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

	/// <summary>The note goes into the release notes: it is there, and it is not "None.".</summary>
	public bool IsListed => !string.IsNullOrEmpty(Note) && !IsNone;

	/// <summary>The tasks added to tasks/done/ since the previous release, by number.</summary>
	public static List<ClosedTask> AddedSince(string previous)
		// Compared with the working tree, so a local preview sees a task moved to done/ and not committed yet.
		=> Git.Run("diff", "--no-renames", "--diff-filter=A", "--name-only", previous, "--", "tasks/done/")
			.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Where(path => path.EndsWith(".md", StringComparison.Ordinal))
			.Select(Read)
			.OrderBy(t => t.Id, StringComparer.Ordinal)
			.ThenBy(t => t.Path, StringComparer.Ordinal)
			.ToList();

	/// <summary>Every closed task has a note; "None." when the users see no change.</summary>
	public static IEnumerable<string> Check(IEnumerable<ClosedTask> tasks)
	{
		foreach (var task in tasks)
		{
			if (task.Note is null)
				yield return $"{task.Path}: no '## Release note' section. Write the note for the users of the packages, or 'None.' when they see no change.";
			else if (task.Note.Length == 0)
				yield return $"{task.Path}: '## Release note' is empty. Write the note, or 'None.' when users see no change.";
			else if (task.Breaking && task.IsNone)
				yield return $"{task.Path}: 'breaking: true' with the note 'None.'. Tell the users what to change, or remove 'breaking: true'.";
		}
	}

	static ClosedTask Read(string path)
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
