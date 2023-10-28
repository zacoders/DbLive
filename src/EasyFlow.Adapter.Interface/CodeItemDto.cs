namespace EasyFlow.Adapter.Interface;

public class CodeItemDto
{
	public required string RelativePath { get; init; }
	public required int ContentHash { get; init; }
	public DateTime AppliedUtc { get; set; }
	public DateTime? VerifiedUtc { get; init; }
	public int ExecutionTimeMs { get; set; }
}