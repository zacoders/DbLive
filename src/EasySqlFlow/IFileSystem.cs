public interface IFileSystem
{
	public IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption);
	public IEnumerable<string> EnumerateFiles(string path, string searchPattern);
}
