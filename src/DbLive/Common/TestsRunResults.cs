namespace DbLive.Common;

internal class TestsRunResults
{
	private int passedCount;
	private int failedCount;

	public int PassedCount => passedCount;
	public int FailedCount => failedCount;

	public int Total => passedCount + failedCount;

	internal void IncremenFailed()
	{
		_ = Interlocked.Increment(ref failedCount);
	}

	internal void IncremenPassed()
	{
		_ = Interlocked.Increment(ref passedCount);
	}
}