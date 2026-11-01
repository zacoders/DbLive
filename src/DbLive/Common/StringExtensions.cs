namespace DbLive.Common;

public static class StringExtensions
{
	/// <summary>
	/// Relative path with '/' on every OS, so the paths stored in the database do not depend on the OS that deployed.
	/// </summary>
	public static string GetRelativePath(this string fullPath, string rootPath)
		=> Path.GetRelativePath(rootPath, fullPath).Replace(Path.DirectorySeparatorChar, '/');
}