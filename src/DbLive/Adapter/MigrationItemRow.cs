namespace DbLive.Adapter;

/// <summary>
/// A <c>dblive.migration</c> row as the providers read it; <c>item_type</c> is mapped by <see cref="MigrationItemTypeMapper"/>.
/// </summary>
[ExcludeFromCodeCoverage]
internal class MigrationItemRow
{
	public long Version { get; init; }
	public string Name { get; init; } = "";
	public string RelativePath { get; init; } = "";
	public string ItemType { get; init; } = "";
	public MigrationItemStatus Status { get; init; }
	public long ContentHash { get; init; }
	public DateTime CreatedUtc { get; init; }
	public DateTime? AppliedUtc { get; init; }
	public long? ExecutionTimeMs { get; init; }

	public MigrationItemDto ToDto() => new()
	{
		Version = Version,
		Name = Name,
		RelativePath = RelativePath,
		ItemType = MigrationItemTypeMapper.FromDbValue(ItemType),
		Status = Status,
		ContentHash = ContentHash,
		CreatedUtc = CreatedUtc,
		AppliedUtc = AppliedUtc,
		ExecutionTimeMs = ExecutionTimeMs
	};
}
