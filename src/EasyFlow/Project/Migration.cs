using System.Collections.ObjectModel;

namespace EasyFlow.Project;

[ExcludeFromCodeCoverage]
public record Migration
{
	public required int Version { get; set; }
	public required string Name { get; set; }
	public required string FolderPath { get; set; }
	public required ReadOnlyCollection<MigrationItem> Items { get; set; }

	public virtual bool Equals(Migration? other)
	{
		if (other is null) return false;
		return Version == other.Version && Name == other.Name;
	}

	public override int GetHashCode()
	{
		//return HashCode.Combine(Version, Name); -- is not available in .net standard 2.0
		int hash = 17;
		hash = hash * 23 + Version.GetHashCode();
		hash = hash * 23 + (Name != null ? Name.GetHashCode() : 0);
		return hash;
	}
}