namespace EasyFlow.Project;

public class FileSystem : IFileSystem
{
	public IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption) =>
		Directory.EnumerateDirectories(path, searchPattern, searchOption);

	public IEnumerable<string> EnumerateDirectories(string[] paths, string searchPattern, SearchOption searchOption)
	{
		return paths.SelectMany(path =>
		{
			if (Path.Exists(path))
				return Directory.EnumerateDirectories(path, searchPattern, searchOption);
			return Enumerable.Empty<string>();
		});
	}

	public IEnumerable<string> EnumerateFiles(string path, string searchPattern, bool subfolders) =>
		Directory.EnumerateFiles(path, searchPattern, subfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);

	public IEnumerable<string> EnumerateFiles(string path, string searchPattern, string excludePattern, bool subfolders)
	{
		var searchOption = subfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

		var files = Directory.EnumerateFiles(path, searchPattern, searchOption);

		var excludeFiles = Directory.EnumerateFiles(path, excludePattern, searchOption);

		return files.Except(excludeFiles);
	}

	public bool FileExists(string path) => File.Exists(path);
	public bool PathExists(string path) => Path.Exists(path);

	public string FileReadAllText(string path) => File.ReadAllText(path);

	public FileData ReadFileData(string path, string rootPath)
	{
		return new FileData 
		{
			Content = File.ReadAllText(path),
			FilePath = path,
			RelativePath = path.GetRelativePath(rootPath)
		};
	}
}