namespace EasyFlow.Project;

public record MigrationTask
{
	public required MigrationType MigrationType { get; set; }
	public required Uri FileUri { get; set; }

	public virtual bool Equals(MigrationTask? other)
	{
		if (other is null) return false;
		return MigrationType == other.MigrationType;
	}

	public override int GetHashCode()
	{
		return MigrationType.GetHashCode();
	}
}