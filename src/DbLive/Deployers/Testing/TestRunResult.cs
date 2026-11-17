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

	internal long ExecutionTimeMs { get; set; }

	public Exception? Exception { get; internal set; }
	internal string Output { get; set; } = "";
}
