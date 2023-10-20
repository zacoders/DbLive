namespace EasyFlow.Adapter.Interface;

public class MigrationDto
{
	public int Version { get; set; }
	public required string Name { get; set; }
	public DateTime CreatedUtc { get; set; }
	public int ExecutionTimeMs { get; set; }
}