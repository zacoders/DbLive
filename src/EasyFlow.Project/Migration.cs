namespace EasyFlow.Project;

[ExcludeFromCodeCoverage]
public record Migration
{
	public required int Version { get; set; }
	public required string Name { get; set; }
	public required string FolderPath { get; set; }
	public required HashSet<MigrationItem> Tasks { get; set; }

	public virtual bool Equals(Migration? other)
	{
		if (other is null) return false;
		return Version == other.Version && Name == other.Name;
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(Version, Name);
	}
}