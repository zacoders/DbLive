namespace DbLive.Common;

public static class StringExtensions
{
	/// <summary>
	/// Relative path with '/' on every OS, so the paths stored in the database do not depend on the OS that deployed.
	/// </summary>
	public static string GetRelativePath(this string fullPath, string rootPath)
		=> Path.GetRelativePath(rootPath, fullPath).Replace(Path.DirectorySeparatorChar, '/');

	/// <summary>
	/// The relative path as the <c>dblive</c> tables store it: in lower case, so a file is identified ignoring case, the way DbLive
	/// finds it in the project. Lower-cased here for every path DbLive writes and looks up, not with the database's lower(): the same
	/// on every database, collation and locale, and a query compares the stored column with a plain value, so its key is used.
	/// </summary>
	internal static string ToStoredPath(this string relativePath) => relativePath.ToLowerInvariant();
}