namespace DbLive.Common;

public static class StringExtensions
{
	public static string CombineWith(this string path, string anotherPath) => Path.Combine(path, anotherPath);

	//public static string GetRelativePath(this string fullPath, string rootPath)
	//	=> Path.GetRelativePath(rootPath, fullPath); -- Path.GetRelativePath method is available in .NET 5.0 and later versions

	public static string GetRelativePath(this string fullPath, string rootPath)
	{
		// Calculate the relative path using Uri
		Uri rootUri = new(rootPath + Path.DirectorySeparatorChar);
		Uri fullUri = new(fullPath);
		return Uri.UnescapeDataString(rootUri.MakeRelativeUri(fullUri).ToString());
	}
}