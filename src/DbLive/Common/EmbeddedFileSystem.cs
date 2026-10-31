using System.IO.Enumeration;
using System.Reflection;
using System.Text;

namespace DbLive.Common;

/// <summary>
/// Scripts embedded in the SQL project assembly as resources named <c>DbLive/&lt;path relative to Scripts&gt;</c>.
/// Paths are returned under a virtual root and joined with the OS separator, like <see cref="FileSystem"/> does,
/// so the project reads the same relative paths and the same content from the assembly as from the scripts folder.
/// </summary>
internal sealed class EmbeddedFileSystem : IFileSystem
{
	internal const string ResourcePrefix = "DbLive/";

	private static readonly char Separator = Path.DirectorySeparatorChar;

	private static readonly Comparer<string> EnumerationOrder = Comparer<string>.Create(CompareEnumerationOrder);

	private readonly Func<string, Stream?> _openResource;

	// file path under the root -> resource name
	private readonly Dictionary<string, string> _files;

	/// <param name="root">Virtual root of the returned paths.</param>
	/// <param name="resourceNames">All resource names; names without the <see cref="ResourcePrefix"/> are ignored.</param>
	/// <param name="openResource">Opens a resource by its name.</param>
	public EmbeddedFileSystem(string root, IEnumerable<string> resourceNames, Func<string, Stream?> openResource)
	{
		Root = Normalize(root);
		_openResource = openResource;
		_files = resourceNames
			.Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
			// %(RecursiveDir) uses '\' when the project is built on Windows.
			.ToDictionary(
				name => Root + Separator + Normalize(name[ResourcePrefix.Length..]),
				name => name,
				StringComparer.OrdinalIgnoreCase
			);
	}

	public static EmbeddedFileSystem FromAssembly(Assembly assembly) =>
		new($"{assembly.GetName().Name}.dll", assembly.GetManifestResourceNames(), assembly.GetManifestResourceStream);

	public string Root { get; }

	public bool IsEmpty => _files.Count == 0;

	public IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption)
	{
		string directory = Normalize(path);

		// there are no empty directories in resources, so the directories are derived from the file paths.
		return _files.Keys
			.Select(filePath => GetSubPath(filePath, directory))
			.OfType<string>()
			.SelectMany(GetParentDirectories)
			.Where(subPath => searchOption == SearchOption.AllDirectories || !subPath.Contains(Separator))
			.Where(subPath => Matches(searchPattern, Path.GetFileName(subPath)))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.Order(EnumerationOrder)
			.Select(subPath => directory + Separator + subPath)
			.ToList();
	}

	public IEnumerable<string> EnumerateDirectories(IEnumerable<string> paths, string searchPattern, SearchOption searchOption) =>
		paths.SelectMany(path => EnumerateDirectories(path, searchPattern, searchOption)).Distinct();

	public IEnumerable<string> EnumerateFiles(string path, string searchPattern, bool subfolders = false) =>
		EnumerateFiles(path, [searchPattern], subfolders);

	public IEnumerable<string> EnumerateFiles(string path, IEnumerable<string> searchPatterns, bool subfolders)
	{
		string directory = Normalize(path);

		List<string> subPaths =
		[
			.. _files.Keys
				.Select(filePath => GetSubPath(filePath, directory))
				.OfType<string>()
				.Where(subPath => subfolders || !subPath.Contains(Separator))
				.Order(EnumerationOrder)
		];

		// pattern by pattern, like FileSystem does.
		return searchPatterns
			.SelectMany(pattern => subPaths.Where(subPath => Matches(pattern, Path.GetFileName(subPath))))
			.Distinct()
			// the directory as the caller passed it, like Directory.EnumerateFiles returns.
			.Select(subPath => directory + Separator + subPath)
			.ToList();
	}

	public IEnumerable<string> EnumerateFiles(string path, string searchPattern, string excludePattern, bool subfolders) =>
		EnumerateFiles(path, [searchPattern], [excludePattern], subfolders);

	public IEnumerable<string> EnumerateFiles(string path, IEnumerable<string> searchPatterns, IEnumerable<string> excludePatterns, bool subfolders)
	{
		IEnumerable<string> files = EnumerateFiles(path, searchPatterns, subfolders);
		IEnumerable<string> excludeFiles = EnumerateFiles(path, excludePatterns, subfolders);
		return files.Except(excludeFiles);
	}

	public bool FileExists(string path) => _files.ContainsKey(Normalize(path));

	public bool PathExists(string path)
	{
		string directory = Normalize(path);
		return _files.Keys.Any(filePath => GetSubPath(filePath, directory) is not null);
	}

	public bool IsDirectoryEmpty(string path) => !PathExists(path);

	public bool PathExistsAndNotEmpty(string path) => PathExists(path);

	public async Task<string> FileReadAllTextAsync(string path)
	{
		using StreamReader reader = OpenReader(path);
		return await reader.ReadToEndAsync().ConfigureAwait(false);
	}

	public async Task<string[]> FileReadAllLinesAsync(string path)
	{
		using StreamReader reader = OpenReader(path);

		List<string> lines = [];
		while (await reader.ReadLineAsync().ConfigureAwait(false) is string line)
		{
			lines.Add(line);
		}
		return [.. lines];
	}

	public async Task<FileData> ReadFileDataAsync(string path, string rootPath)
	{
		return new FileData
		{
			Content = await FileReadAllTextAsync(path).ConfigureAwait(false),
			FilePath = path,
			RelativePath = GetSubPath(Normalize(path), Normalize(rootPath))?.Replace(Separator, '/')
				?? path.GetRelativePath(rootPath)
		};
	}

	/// <summary>
	/// Decodes like File.ReadAllTextAsync, so content hashes do not depend on where the scripts are read from.
	/// </summary>
	private StreamReader OpenReader(string path)
	{
		if (!_files.TryGetValue(Normalize(path), out string? resourceName))
		{
			throw new FileNotFoundException($"Script '{path}' is not embedded.", path);
		}

		Stream stream = _openResource(resourceName)
			?? throw new FileNotFoundException($"Resource '{resourceName}' cannot be read.", path);

		return new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
	}

	private static string Normalize(string path) =>
		path.Replace('\\', Separator).Replace('/', Separator).TrimEnd(Separator);

	/// <summary>
	/// The part of <paramref name="path"/> under <paramref name="directory"/>, or null when it is not under it.
	/// </summary>
	private static string? GetSubPath(string path, string directory) =>
		path.Length > directory.Length + 1
			&& path[directory.Length] == Separator
			&& path.StartsWith(directory, StringComparison.OrdinalIgnoreCase)
				? path[(directory.Length + 1)..]
				: null;

	private static IEnumerable<string> GetParentDirectories(string subPath)
	{
		for (int i = subPath.IndexOf(Separator); i >= 0; i = subPath.IndexOf(Separator, i + 1))
		{
			yield return subPath[..i];
		}
	}

	/// <summary>
	/// The order of Directory.EnumerateFiles on Windows: breadth-first, the entries of each folder sorted by name.
	/// </summary>
	private static int CompareEnumerationOrder(string x, string y)
	{
		string[] xParts = x.Split(Separator);
		string[] yParts = y.Split(Separator);

		if (xParts.Length != yParts.Length)
		{
			return xParts.Length.CompareTo(yParts.Length);
		}

		for (int i = 0; i < xParts.Length; i++)
		{
			int result = StringComparer.OrdinalIgnoreCase.Compare(xParts[i], yParts[i]);
			if (result != 0)
			{
				return result;
			}
		}

		return 0;
	}

	/// <summary>
	/// Matches a file name the way Directory.EnumerateFiles does on Windows.
	/// </summary>
	private static bool Matches(string pattern, string name) =>
		FileSystemName.MatchesWin32Expression(FileSystemName.TranslateWin32Expression(pattern), name, ignoreCase: true);
}
