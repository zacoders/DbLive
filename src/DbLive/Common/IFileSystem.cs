namespace DbLive.Common;

public interface IFileSystem
{
	//todo: add docs for the methods

	IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption);
	IEnumerable<string> EnumerateDirectories(IEnumerable<string> paths, string searchPattern, SearchOption searchOption);
	IEnumerable<string> EnumerateFiles(string path, string searchPattern, bool subfolders = false);
	IEnumerable<string> EnumerateFiles(string path, IEnumerable<string> searchPatterns, bool subfolders);
	IEnumerable<string> EnumerateFiles(string path, string searchPattern, string excludePattern, bool subfolders);
	IEnumerable<string> EnumerateFiles(string path, IEnumerable<string> searchPatterns, IEnumerable<string> excludePatterns, bool subfolders);
	Task<FileData> ReadFileDataAsync(string path, string rootPath);
	Task<string> FileReadAllTextAsync(string path);
	Task<string[]> FileReadAllLinesAsync(string path);
	bool FileExists(string path);
	bool PathExists(string path);
	bool IsDirectoryEmpty(string path);
	bool PathExistsAndNotEmpty(string path);
}
