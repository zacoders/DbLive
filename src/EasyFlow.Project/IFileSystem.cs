namespace EasyFlow.Project;

public interface IFileSystem
{
	IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption);
	IEnumerable<string> EnumerateDirectories(string[] paths, string searchPattern, SearchOption searchOption);
	IEnumerable<string> EnumerateFiles(string path, string searchPattern, bool subfolders = false);
	IEnumerable<string> EnumerateFiles(string path, string searchPattern, string excludePattern, bool subfolders);
	string FileReadAllText(string path);
	bool FileExists(string path);
}
