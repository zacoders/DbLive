using System.Diagnostics;

namespace EasyFlow.Common;

public interface ITimeProvider
{
	DateTime UtcNow();
	IStopWatch StartNewStopwatch();
}
