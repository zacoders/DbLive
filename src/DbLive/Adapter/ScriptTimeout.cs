namespace DbLive.Adapter;

/// <summary>
/// The timeout of a script counts from the start of its first command, not per command: on SQL Server a script is several <c>GO</c>
/// batches, each a command of its own, and each gets the time left of the script as its command timeout.
/// </summary>
internal sealed class ScriptTimeout
{
	private readonly Func<TimeSpan> _elapsed;

	public TimeSpan Timeout { get; }

	internal ScriptTimeout(TimeSpan timeout, Func<TimeSpan> elapsed)
	{
		Timeout = timeout;
		_elapsed = elapsed;
	}

	/// <summary>
	/// Starts the clock of a script, right before its first command.
	/// </summary>
	public static ScriptTimeout Start(TimeSpan timeout)
	{
		long started = Stopwatch.GetTimestamp();
		return new ScriptTimeout(timeout, () => Stopwatch.GetElapsedTime(started));
	}

	/// <summary>
	/// The command timeout of the next command of the script, in seconds.
	/// </summary>
	/// <exception cref="ExecutionTimeoutException">The time of the script ran out before this command.</exception>
	public int GetCommandTimeoutSeconds()
	{
		return GetCommandTimeoutSeconds(Timeout, _elapsed()) ?? throw new ExecutionTimeoutException(Timeout);
	}

	/// <summary>
	/// The timeout error of the provider for a command of the script, as the timeout of the script.
	/// </summary>
	public ExecutionTimeoutException Expired(Exception providerException) => new(Timeout, providerException);

	/// <summary>
	/// The time left in whole seconds, rounded up: a command timeout of 0 is no limit, so the time left never becomes 0 by rounding,
	/// and a batch may finish up to a second after the end of the time of the file. 0 for a timeout of 00:00:00, which is no limit.
	/// Null when no time is left.
	/// </summary>
	internal static int? GetCommandTimeoutSeconds(TimeSpan timeout, TimeSpan elapsed)
	{
		if (timeout == TimeSpan.Zero)
		{
			return 0;
		}

		TimeSpan left = timeout - elapsed;
		if (left <= TimeSpan.Zero)
		{
			return null;
		}

		return (int)Math.Min(Math.Ceiling(left.TotalSeconds), int.MaxValue);
	}
}
