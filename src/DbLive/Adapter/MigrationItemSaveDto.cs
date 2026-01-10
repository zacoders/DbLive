namespace DbLive.Adapter;

[ExcludeFromCodeCoverage]
public class MigrationItemSaveDto
{
	public required int Version { get; init; }
	public required string Name { get; init; }
	public required string RelativePath { get; init; }
	public required MigrationItemType ItemType { get; init; }
	public required MigrationItemStatus Status { get; init; }
	public required long ContentHash { get; init; }
	public string? Content { get; set; }
	public DateTime CreatedUtc { get; init; }
}
