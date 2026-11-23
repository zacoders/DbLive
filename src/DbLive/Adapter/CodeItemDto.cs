namespace DbLive.Adapter;

/// <summary>
/// A <c>dblive.code</c> row. The query that reads it names its columns after the properties: Dapper maps them by name, and a column it
/// does not match leaves the property empty.
/// </summary>
[ExcludeFromCodeCoverage]
internal class CodeItemDto
{
	public required string RelativePath { get; set; }
	public required CodeItemType ItemType { get; set; }
	public required long ContentHash { get; set; }
	public required CodeItemStatus Status { get; set; }
	public DateTime AppliedUtc { get; set; }
	public DateTime CreatedUtc { get; set; }
	public DateTime? VerifiedUtc { get; set; }
	public int ExecutionTimeMs { get; set; }
}
