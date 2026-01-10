namespace DbLive.Common;

public class FileSystem : IFileSystem
{
	public IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption) =>
		Directory.EnumerateDirectories(path, searchPattern, searchOption);

	public IEnumerable<string> EnumerateDirectories(IEnumerable<string> paths, string searchPattern, SearchOption searchOption)
	{
		return paths.SelectMany(path =>
		{
			if (Directory.Exists(path))
				return Directory.EnumerateDirectories(path, searchPattern, searchOption);
			return Enumerable.Empty<string>();
		}).Distinct();
	}

	public IEnumerable<string> EnumerateFiles(string path, string searchPattern, bool subfolders) =>
		Directory.EnumerateFiles(path, searchPattern, subfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);

	public IEnumerable<string> EnumerateFiles(string path, IEnumerable<string> searchPatterns, bool subfolders) =>
		searchPatterns.SelectMany(searchPattern =>
			Directory.EnumerateFiles(path, searchPattern, subfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
		).Distinct();

	public IEnumerable<string> EnumerateFiles(string path, IEnumerable<string> searchPatterns, IEnumerable<string> excludePatterns, bool subfolders)
	{
		IEnumerable<string> files = EnumerateFiles(path, searchPatterns, subfolders);
		IEnumerable<string> excludeFiles = EnumerateFiles(path, excludePatterns, subfolders);
		return files.Except(excludeFiles);
	}

	public IEnumerable<string> EnumerateFiles(string path, string searchPattern, string excludePattern, bool subfolders)
	{
		IEnumerable<string> files = EnumerateFiles(path, searchPattern, subfolders);
		IEnumerable<string> excludeFiles = EnumerateFiles(path, excludePattern, subfolders);
		return files.Except(excludeFiles);
	}

	public bool FileExists(string path) => File.Exists(path);

	public bool PathExists(string path) => Directory.Exists(path);

	public bool IsDirectoryEmpty(string path)
	{
		return !Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories).Any();
	}

	public bool PathExistsAndNotEmpty(string path)
	{
		if (!Directory.Exists(path)) return false;
		return !IsDirectoryEmpty(path);
	}

	public Task<string> FileReadAllTextAsync(string path) => File.ReadAllTextAsync(path);
	public Task<string[]> FileReadAllLinesAsync(string path) => File.ReadAllLinesAsync(path);

	public async Task<FileData> ReadFileDataAsync(string path, string rootPath)
	{
		return new FileData
		{
			Content = await File.ReadAllTextAsync(path),
			FilePath = path,
			RelativePath = path.GetRelativePath(rootPath)
		};
	}
}