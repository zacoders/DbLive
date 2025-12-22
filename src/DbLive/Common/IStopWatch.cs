namespace DbLive.Common;

public interface IStopWatch
{
	void Start();
	void Stop();
	long ElapsedMilliseconds { get; }
}

