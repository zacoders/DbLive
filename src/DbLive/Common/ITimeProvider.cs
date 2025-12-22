namespace DbLive.Common;

public interface ITimeProvider
{
	DateTime UtcNow();
	IStopWatch StartNewStopwatch();
}
