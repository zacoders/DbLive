namespace DbLive.Common;

internal interface IStopWatch
{
	void Start();
	void Stop();
	long ElapsedMilliseconds { get; }
}

