namespace DbLive;


/// <summary>
/// The scripts folder has files that DbLive never reads: outside the project folders and not the root settings.json.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class FilesOutsideProjectFoldersException : Exception
{
	internal FilesOutsideProjectFoldersException(int count, IReadOnlyList<string> relativePaths, IReadOnlyList<string> projectFolders)
		: base(
			$"{count} file(s) of the scripts folder are outside the project folders, so they would never be deployed: "
			+ string.Join(", ", relativePaths.Select(p => $"'{p}'"))
			+ (count > relativePaths.Count ? ", ..." : "")
			+ $". DbLive reads only settings.json and the folders {string.Join(", ", projectFolders)}. "
			+ "Rename the folder to one of them (or set its name in settings.json), or move the files out of the scripts folder."
		)
	{
		Count = count;
		RelativePaths = relativePaths;
	}

	/// <summary>
	/// Number of files outside the project folders.
	/// </summary>
	public int Count { get; private set; }

	/// <summary>
	/// The first of these files, by relative path.
	/// </summary>
	public IReadOnlyList<string> RelativePaths { get; private set; }
}
