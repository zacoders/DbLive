namespace DbLive.Common;

internal static class ProjectIdTools
{
	/// <summary>
	/// The length of <c>dblive.version.project_id</c>: <c>nvarchar(128)</c> on SQL Server, <c>varchar(128)</c> on PostgreSQL.
	/// </summary>
	public const int MaxLength = 128;

	public static string Normalize(string projectId) => projectId.ToLowerInvariant();

	public static bool Equals(string? left, string? right) =>
		string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
