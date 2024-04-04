namespace EasyFlow.Deployers.Testing;

[ExcludeFromCodeCoverage]
public class TestRunResult
{
	public bool IsSuccess { get; set; }
	public string? ErrorMessage { get; set; }
	public DateTime StartedUtc { get; set; }
	public DateTime CompletedUtc { get; set; }

	public int ExecutionTimeMs => (int)(CompletedUtc - StartedUtc).TotalMilliseconds;

	public Exception? Exception { get; set; }
	public string Output { get; internal set; } = "";
}
