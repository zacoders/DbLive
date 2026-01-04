namespace DbLive.Adapter;

[ExcludeFromCodeCoverage]
public class MigrationItemStateDto
{
	public required int Version { get; init; }
	public required MigrationItemType ItemType { get; init; }
	public required MigrationItemStatus Status { get; init; }
	public DateTime? AppliedUtc { get; set; }
	public long? ExecutionTimeMs { get; set; }
	public string? ErrorMessage { get; set; }
}