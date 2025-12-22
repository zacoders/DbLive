using System.Diagnostics;

namespace DbLive.Common;

public class StopWatch : IStopWatch
{
	private readonly Stopwatch _stopWatch = new();

	public static StopWatch StartNew()
	{
		StopWatch sw = new();
		sw.Start();
		return sw;
	}

	public void Start()
	{
		_stopWatch.Start();
	}

	public void Stop()
	{
		_stopWatch.Stop();
	}

	public long ElapsedMilliseconds => _stopWatch.ElapsedMilliseconds;
}

