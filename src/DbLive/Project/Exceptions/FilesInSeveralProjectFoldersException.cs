namespace DbLive;

/// <summary>
/// The scripts folder has files in more than one project folder: a project folder inside another one, such as
/// <c>TestsFolder: "Code/Tests"</c>, or <c>CodeFolder: ""</c> next to the other folders. DbLive would read each such file
/// more than once, a migration also as code. Nothing ran.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class FilesInSeveralProjectFoldersException : Exception
{
	internal FilesInSeveralProjectFoldersException(int count, IReadOnlyList<(string RelativePath, IReadOnlyList<string> Folders)> files)
		: base(
			$"{count} file(s) of the scripts folder are in more than one project folder, so DbLive would read each of them more than once: "
			+ string.Join(", ", files.Select(f => $"'{f.RelativePath}' ({string.Join(", ", f.Folders)})"))
			+ (count > files.Count ? ", ..." : "")
			+ ". Change the folder settings in settings.json so that no project folder is inside another one "
			+ "(CodeFolder \"\" is the whole scripts folder), or move the files."
		)
	{
		Count = count;
		RelativePaths = [.. files.Select(f => f.RelativePath)];
	}

	/// <summary>
	/// Number of files in more than one project folder.
	/// </summary>
	public int Count { get; private set; }

	/// <summary>
	/// The first of these files, by relative path.
	/// </summary>
	public IReadOnlyList<string> RelativePaths { get; private set; }
}
