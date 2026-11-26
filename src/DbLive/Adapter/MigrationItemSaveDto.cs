namespace DbLive.Adapter;

[ExcludeFromCodeCoverage]
internal class MigrationItemSaveDto
{
	public required long Version { get; init; }
	public required string Name { get; init; }
	public required string RelativePath { get; init; }
	public required MigrationItemType ItemType { get; init; }
	public required MigrationItemStatus Status { get; init; }
	public required long ContentHash { get; init; }
	public DateTime CreatedUtc { get; init; }
}
