namespace DbLive.Adapter;

[ExcludeFromCodeCoverage]
public class MigrationItemDto
{
	public required int Version { get; init; }
	public required string Name { get; init; }
	public required MigrationItemType ItemType { get; init; }
	public required MigrationItemStatus Status { get; init; }
	public required int ContentHash { get; init; }
	public string Content { get; set; } = string.Empty;
	public DateTime CreatedUtc { get; init; }
	public DateTime? AppliedUtc { get; set; }
	public long? ExecutionTimeMs { get; set; }
}