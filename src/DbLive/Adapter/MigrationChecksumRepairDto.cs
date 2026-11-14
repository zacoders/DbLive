namespace DbLive.Adapter;

[ExcludeFromCodeCoverage]
internal class MigrationChecksumRepairDto
{
	public required long Version { get; init; }
	public required MigrationItemType ItemType { get; init; }
	public required long ContentHash { get; init; }
	public required string RelativePath { get; init; }
}
