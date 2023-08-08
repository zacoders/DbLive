internal static class UriHelpers
{
	public static string GetFolder(this Uri folderUri)
	{
		return folderUri.Segments[^1];
	}
}