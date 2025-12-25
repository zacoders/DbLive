namespace DbLive.Adapter;

[ExcludeFromCodeCoverage]
public class MigrationDto
{
	public int Version { get; set; }
	public DateTime CreatedUtc { get; set; }
	public DateTime ModifiedUtc { get; set; }
}