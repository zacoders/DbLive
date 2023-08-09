namespace EasySqlFlow;

public record Migration
{
	public required int Version { get; set; }
	public required string Name { get; set; }
	public required Uri PathUri { get; set; }
	public required HashSet<MigrationTask> Tasks { get; set; }

	public virtual bool Equals(Migration? other)
	{
		if (other is null) return false;
		return Version == other.Version && Name == other.Name;
	}

	public override int GetHashCode()
	{
		return System.HashCode.Combine(Version, Name);
	}
}