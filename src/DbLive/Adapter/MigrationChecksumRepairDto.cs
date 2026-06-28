namespace DbLive.Adapter;

[ExcludeFromCodeCoverage]
public class MigrationChecksumRepairDto
{
	public required long Version { get; init; }
	public required MigrationItemType ItemType { get; init; }
	public required long ContentHash { get; init; }
	public string? Content { get; init; }
	public required string RelativePath { get; init; }
}
