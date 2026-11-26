namespace DbLive.Common;

internal static class ProjectIdTools
{
	/// <summary>
	/// The length of <c>dblive.version.project_id</c>: <c>nvarchar(128)</c> on SQL Server, <c>varchar(128)</c> on PostgreSQL.
	/// </summary>
	public const int MaxLength = 128;

	public static string Normalize(string projectId) => projectId.ToLowerInvariant();

	/// <summary>
	/// Compares the stored forms, not with <see cref="StringComparison.OrdinalIgnoreCase"/>: for a few letters the two differ
	/// (<c>ẞ</c> is stored as <c>ß</c>, the Kelvin sign as <c>k</c>), and a stored value would not equal its own ProjectId.
	/// </summary>
	public static bool Equals(string? left, string? right) =>
		string.Equals(left is null ? null : Normalize(left), right is null ? null : Normalize(right), StringComparison.Ordinal);
}
