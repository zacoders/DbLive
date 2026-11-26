namespace DbLive.Adapter;

/// <summary>
/// The timeout of a script counts from the start of its first command, not per command: on SQL Server a script is several <c>GO</c>
/// batches, each a command of its own, and each gets the time left of the script as its command timeout. On PostgreSQL the script is
/// one command, and DbLive cancels it when the time runs out (<see cref="CreateCancellation"/>).
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
	/// A token that is cancelled when the time of the script runs out, for a provider whose command timeout does not limit the whole
	/// script: Npgsql starts its command timeout again on every notice of the server.
	/// </summary>
	public CancellationTokenSource CreateCancellation()
	{
		CancellationTokenSource cancellation = new();

		if (GetCancelDelay(Timeout, _elapsed()) is TimeSpan delay)
		{
			cancellation.CancelAfter(delay);
		}

		return cancellation;
	}

	/// <summary>
	/// The error of the provider when the time of the script ran out, as the timeout of the script: the command timeout, or the cancel of
	/// the command by <see cref="CreateCancellation"/>.
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

	/// <summary>
	/// The longest delay that <see cref="CancellationTokenSource.CancelAfter(TimeSpan)"/> takes, about 49.7 days: a longer one throws
	/// <see cref="ArgumentOutOfRangeException"/>.
	/// </summary>
	internal static readonly TimeSpan MaxCancelDelay = TimeSpan.FromMilliseconds(uint.MaxValue - 1);

	/// <summary>
	/// The time left until the script is cancelled; <see cref="TimeSpan.Zero"/>, cancel at once, when no time is left. Null for no limit:
	/// a timeout of 00:00:00, or a time left longer than <see cref="MaxCancelDelay"/>, which no deployment reaches.
	/// </summary>
	internal static TimeSpan? GetCancelDelay(TimeSpan timeout, TimeSpan elapsed)
	{
		if (timeout == TimeSpan.Zero)
		{
			return null;
		}

		TimeSpan left = timeout - elapsed;
		if (left <= TimeSpan.Zero)
		{
			return TimeSpan.Zero;
		}

		return left <= MaxCancelDelay ? left : null;
	}
}
