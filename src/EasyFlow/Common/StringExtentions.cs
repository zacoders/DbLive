namespace EasyFlow.Common;

public static class StringExtentions
{
	public static string CombineWith(this string path, string anotherPath) => Path.Combine(path, anotherPath);

	public static string GetLastSegment(this string path)
	{
		return GetSegmentFromTheEnd(path, 1);
	}

	/// <summary>
	/// Gets path segment, counting from the end.
	/// </summary>
	/// <param name="path"></param>
	/// <param name="segmentNumger">Segment numger counted from the end.</param>
	/// <returns>Segment</returns>
	public static string GetSegmentFromTheEnd(this string path, int segmentNumger)
	{
		Uri folderUri = new(path);
		return folderUri.Segments[folderUri.Segments.Length - segmentNumger];
	}

	//public static string GetRelativePath(this string fullPath, string rootPath)
	//	=> Path.GetRelativePath(rootPath, fullPath); -- Path.GetRelativePath method is available in .NET 5.0 and later versions

	public static string GetRelativePath(this string fullPath, string rootPath)
	{
		// Calculate the relative path using Uri
		Uri rootUri = new Uri(rootPath + Path.DirectorySeparatorChar);
		Uri fullUri = new Uri(fullPath);
		return Uri.UnescapeDataString(rootUri.MakeRelativeUri(fullUri).ToString());
	}
}