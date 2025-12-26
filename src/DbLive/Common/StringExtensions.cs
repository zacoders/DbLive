namespace DbLive.Common;

public static class StringExtensions
{
	public static string CombineWith(this string path, string anotherPath) => Path.Combine(path, anotherPath);

	public static string GetRelativePath(this string fullPath, string rootPath)
		=> Path.GetRelativePath(rootPath, fullPath);
}