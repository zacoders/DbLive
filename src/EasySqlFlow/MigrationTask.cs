namespace EasySqlFlow;

public class MigrationTask
{
	public required MigrationType MigrationType { get; set; }
	public required string Name { get; set; }
	public required Uri FileUri { get; set; }

	public virtual bool Equals(MigrationTask? other)
	{
		if (other is null) return false;
		return MigrationType == other.MigrationType && Name == other.Name;
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(MigrationType, Name);
	}
}