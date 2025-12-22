namespace EasyFlow.Adapter;

[ExcludeFromCodeCoverage]
public class MigrationDto
{
	public int Version { get; set; }
	public required string Name { get; set; }
	public DateTime CreatedUtc { get; set; }
	public DateTime ModifiedUtc { get; set; }
}