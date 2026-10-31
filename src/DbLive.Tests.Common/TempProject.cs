namespace DbLive.Tests.Common;

/// <summary>
/// A DbLive scripts folder in the temp directory, deleted on dispose.
/// </summary>
public sealed class TempProject : IDisposable
{
	public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dblive-tests", Guid.NewGuid().ToString());

	public IProjectPath ProjectPath => new ProjectPath(Path, Path);

	public TempProject AddFile(string relativePath, string content)
	{
		string filePath = System.IO.Path.Combine(Path, relativePath);
		_ = Directory.CreateDirectory(System.IO.Path.GetDirectoryName(filePath)!);
		File.WriteAllText(filePath, content);
		return this;
	}

	public TempProject DeleteFiles(string searchPattern)
	{
		foreach (string filePath in Directory.EnumerateFiles(Path, searchPattern, SearchOption.AllDirectories))
		{
			File.Delete(filePath);
		}
		return this;
	}

	public void Dispose()
	{
		if (Directory.Exists(Path))
		{
			Directory.Delete(Path, recursive: true);
		}
	}
}
