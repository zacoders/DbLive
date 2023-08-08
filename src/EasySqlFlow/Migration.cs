public record Migration
{
	public required int Version { get; set; }
	public required string Name { get; set; }
	public required Uri PathUri { get; set; }

	public virtual bool Equals(Migration? other)
	{
		if (other is null)
		{
			return false;
		}

		return Version == other.Version && Name == other.Name;
	}

	public override int GetHashCode()
	{
		int hash = 17;
		hash = hash * 23 + Version.GetHashCode();
		hash = hash * 23 + (Name != null ? Name.GetHashCode() : 0);
		return hash;
	}
}