public class FileSystem : IFileSystem
{
	public IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption)
	{
		return Directory.EnumerateDirectories(path, searchPattern, searchOption);
	}
}