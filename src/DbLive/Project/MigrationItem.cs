namespace DbLive.Project;

[ExcludeFromCodeCoverage]
public record MigrationItem
{
	public required MigrationItemType MigrationItemType { get; set; }
	public required FileData FileData { get; set; }
	public string Name { get; set; } = string.Empty;

	public virtual bool Equals(MigrationItem? other)
	{
		if (other is null) return false;
		return MigrationItemType == other.MigrationItemType;
	}

	public override int GetHashCode()
	{
		return MigrationItemType.GetHashCode();
	}
}