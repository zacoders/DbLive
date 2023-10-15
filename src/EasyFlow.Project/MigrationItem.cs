namespace EasyFlow.Project;

public record MigrationItem
{
	public required MigrationType MigrationType { get; set; }
	public required FileData FileData { get; set; }

	public virtual bool Equals(MigrationItem? other)
	{
		if (other is null) return false;
		return MigrationType == other.MigrationType;
	}

	public override int GetHashCode()
	{
		return MigrationType.GetHashCode();
	}
}