namespace EasyFlow.Common;

public static class StringExtentions
{
	public static string CombineWith(this string path, string anotherPath) => Path.Combine(path, anotherPath);

	public static string GetLastSegment(this string path)
	{
		Uri folderUri = new(path);
		return folderUri.Segments[^1];
	}

	public static string GetRelativePath(this string fullPath, string rootPath)
		=> Path.GetRelativePath(rootPath, fullPath);
}