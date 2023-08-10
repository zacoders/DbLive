namespace EasyFlow.Project;

internal static class UriHelpers
{
	public static string GetLastSegment(this Uri folderUri)
	{
		return folderUri.Segments[^1];
	}
}