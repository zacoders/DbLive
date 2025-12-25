namespace DbLive.Project;

[ExcludeFromCodeCoverage]
public record Migration
{
	public required int Version { get; set; }
	public required Dictionary<MigrationItemType, MigrationItem> Items { get; set; }

	public virtual bool Equals(Migration? other)
	{
		if (other is null) return false;
		return Version == other.Version;
	}

	public override int GetHashCode()
	{
		return Version;
	}
}