namespace EasyFlow.Adapter;

[ExcludeFromCodeCoverage]
public class CodeItemDto
{
	public required string RelativePath { get; set; }
	public required int ContentHash { get; set; }
	public DateTime AppliedUtc { get; set; }
	public DateTime? VerifiedUtc { get; set; }
	public int ExecutionTimeMs { get; set; }
}