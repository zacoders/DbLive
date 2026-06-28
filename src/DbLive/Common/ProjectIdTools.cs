namespace DbLive.Common;

internal static class ProjectIdTools
{
	public static string Normalize(string projectId) => projectId.ToLowerInvariant();

	public static bool Equals(string? left, string? right) =>
		string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
