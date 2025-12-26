
namespace DbLive.Project;

[ExcludeFromCodeCoverage]
internal record MigrationItemInfo
{
	public required int Version { get; init; }
	public required string Name { get; init; }
	public required string FilePath { get; init; }
	public required MigrationItemType MigrationItemType { get; init; }
}
