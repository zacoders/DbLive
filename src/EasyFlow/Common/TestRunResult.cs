namespace EasyFlow.Common;

internal class TestRunResult
{
	private int passedCount;
	private int failedCount;

	public int PassedCount => passedCount;
	public int FailedCount => failedCount;

	public int Total => passedCount + failedCount;

	internal void IncremenFailed()
	{
		Interlocked.Increment(ref failedCount);
	}

	internal void IncremenPassed()
	{
		Interlocked.Increment(ref passedCount);
	}
}