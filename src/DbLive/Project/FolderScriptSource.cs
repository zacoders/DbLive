using System.Text;

namespace DbLive.Project;

/// <summary>
/// Scripts in a folder.
/// </summary>
/// <param name="path">Full path of the folder.</param>
internal sealed class FolderScriptSource(string path) : IScriptSource
{
	public string Location => path;

	/// <summary>
	/// Enumerates the folder on every call. The project reads it once per builder (<see cref="DbLiveProject"/>).
	/// </summary>
	public IReadOnlyList<string> GetFiles() =>
		[.. Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Select(filePath => filePath.GetRelativePath(path))];

	/// <summary>
	/// Decodes as <see cref="ScriptEncoding"/> says (File.ReadAllTextAsync detects a byte order mark).
	/// </summary>
	/// <exception cref="FileNotUtf8Exception">The file has bytes that are not UTF-8.</exception>
	public async Task<string> ReadAsync(string relativePath)
	{
		try
		{
			return await File.ReadAllTextAsync(Path.Combine(path, relativePath), ScriptEncoding.StrictUtf8).ConfigureAwait(false);
		}
		catch (DecoderFallbackException ex)
		{
			throw new FileNotUtf8Exception(relativePath, Location, ex);
		}
	}
}
