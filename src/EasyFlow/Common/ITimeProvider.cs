namespace EasyFlow.Common;

public interface ITimeProvider
{
	DateTime UtcNow();
	IStopWatch StartNewStopwatch();
}
