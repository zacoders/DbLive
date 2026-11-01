namespace DbLive.Project;

/// <summary>
/// Scripts in a folder.
/// </summary>
/// <param name="path">Full path of the folder.</param>
internal sealed class FolderScriptSource(string path) : IScriptSource
{
	public string Location => path;

	/// <summary>
	/// Enumerates the folder on every call, so files added or deleted after the builder was created are seen.
	/// </summary>
	public IReadOnlyList<string> GetFiles() =>
		[.. Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Select(filePath => filePath.GetRelativePath(path))];

	public Task<string> ReadAsync(string relativePath) => File.ReadAllTextAsync(Path.Combine(path, relativePath));
}
