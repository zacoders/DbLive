namespace DbLive.Common;

public static class StringExtensions
{
	public static string CombineWith(this string path, string anotherPath) => Path.Combine(path, anotherPath);

	/// <summary>
	/// Combines with a relative path from the settings, which may use either '\' or '/'.
	/// Enumerated files use the OS separator, so the relative path is normalized to it.
	/// Leading and trailing separators are removed, so the relative path is never treated as rooted.
	/// </summary>
	public static string CombineWithRelative(this string path, string relativePath) =>
		Path.Combine(
			path,
			relativePath
				.Replace('\\', Path.DirectorySeparatorChar)
				.Replace('/', Path.DirectorySeparatorChar)
				.Trim(Path.DirectorySeparatorChar)
		);

	/// <summary>
	/// Relative path with '/' on every OS, so the paths stored in the database do not depend on the OS that deployed.
	/// </summary>
	public static string GetRelativePath(this string fullPath, string rootPath)
		=> Path.GetRelativePath(rootPath, fullPath).Replace(Path.DirectorySeparatorChar, '/');
}