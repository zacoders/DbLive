
namespace DbLive.Project;

[ExcludeFromCodeCoverage]
public record MigrationItemInfo
{
	public required long Version { get; init; }
	public required string Name { get; init; }
	public required string FilePath { get; init; }
	public required MigrationItemType MigrationItemType { get; init; }
}
