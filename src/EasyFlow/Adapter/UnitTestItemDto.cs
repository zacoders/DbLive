namespace EasyFlow.Adapter;

[ExcludeFromCodeCoverage]
public class UnitTestItemDto
{
	public required string RelativePath { get; init; }
	public required int Crc32Hash { get; init; }
	public required DateTime StartedUtc { get; init; }
	public required long ExecutionTimeMs { get; init; }
	public required bool IsSuccess { get; init; }
	public string? ErrorMessage { get; init; }
}