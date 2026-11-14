namespace DbLive.Testing;

[ExcludeFromCodeCoverage]
public sealed class TestRunResult
{
	internal TestRunResult()
	{
	}

	public bool IsSuccess { get; internal set; }
	public string? ErrorMessage { get; internal set; }
	internal DateTime StartedUtc { get; set; }
	internal DateTime CompletedUtc { get; set; }

	public long ExecutionTimeMs { get; internal set; }

	public Exception? Exception { get; internal set; }
	public string Output { get; internal set; } = "";
}
