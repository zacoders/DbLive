namespace DbLive.Adapter;

[ExcludeFromCodeCoverage]
public class CodeItemDto
{
	public required string RelativePath { get; set; }
	public required int ContentHash { get; set; }
	public required CodeItemStatus Status { get; set; }
	public DateTime AppliedUtc { get; set; }
	public DateTime CreatedUtc { get; set; }
	public DateTime? VerifiedUtc { get; set; }
	public int ExecutionTimeMs { get; set; }
	public string? ErrorMessage { get; set; }
}
