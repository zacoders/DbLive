namespace EasyFlow.Common;

public static class StringExtentions
{
	public static string CombineWith(this string path, string anotherPath)
	{
		return Path.Combine(path, anotherPath);
	}

	public static string GetLastSegment(this string path)
	{
		Uri folderUri = new Uri(path);
		return folderUri.Segments[^1];
	}

	public static Uri ToUri(this string path)
	{
		return new Uri(path);
	}
}