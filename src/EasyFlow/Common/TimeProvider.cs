namespace EasyFlow.Common;

public class TimeProvider : ITimeProvider
{
	public DateTime UtcNow() => DateTime.UtcNow; // this is the only place in the app where we get current utc time.
}