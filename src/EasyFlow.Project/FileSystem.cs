namespace EasyFlow.Project;

public class FileSystem : IFileSystem
{
	public IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption) =>
		Directory.EnumerateDirectories(path, searchPattern, searchOption);

	public IEnumerable<string> EnumerateFiles(string path, string searchPattern) =>
		Directory.EnumerateFiles(path, searchPattern);

	public bool FileExists(string path) => File.Exists(path);

	public string FileReadAllText(string path) => File.ReadAllText(path);
}