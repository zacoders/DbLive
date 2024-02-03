namespace EasyFlow.Adapter;

[ExcludeFromCodeCoverage]
public class MigrationItemDto
{
	public required int Version { get; init; }
	public required string Name { get; init; }
	public required string ItemType { get; init; }
	public required string Status { get; init; }
	public required int ContentHash { get; init; }
	public string Content { get; set; } = string.Empty;
	public DateTime CreatedUtc { get; init; }
	public DateTime? AppliedUtc { get; set; }
	public int? ExecutionTimeMs { get; set; }
}