namespace DbLive.Common;

internal interface ITimeProvider
{
	DateTime UtcNow();
	IStopWatch StartNewStopwatch();
}
