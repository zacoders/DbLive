namespace DbLive.Common;

public static class TransactionScopeManager
{
	private static readonly object _maximumTimeoutLock = new();

	public static TransactionScope Create(TranIsolationLevel isolationLevel, TimeSpan timeOut)
	{
		EnsureMaximumTimeout(timeOut);

		TransactionOptions _options = new()
		{
			IsolationLevel = TranIsolationLevelMapper.ToSystemTransaction(isolationLevel),
			Timeout = timeOut
		};
		return new TransactionScope(
			TransactionScopeOption.Required,
			_options,
			TransactionScopeAsyncFlowOption.Enabled
		);
	}

	public static TransactionScope Create()
		=> Create(TranIsolationLevel.ReadCommitted, TimeSpan.FromMinutes(1));

	/// <summary>
	/// .NET silently caps every scope timeout at <see cref="TransactionManager.MaximumTimeout"/> (10 minutes by default),
	/// so the timeout settings would not work without raising it. The cap is only raised, never lowered.
	/// <see cref="TimeSpan.Zero"/> means no limit for both the scope timeout and the cap.
	/// </summary>
	internal static void EnsureMaximumTimeout(TimeSpan timeout)
	{
		lock (_maximumTimeoutLock)
		{
			TimeSpan maximumTimeout = TransactionManager.MaximumTimeout;

			if (maximumTimeout == TimeSpan.Zero) return;

			if (timeout == TimeSpan.Zero || timeout > maximumTimeout)
			{
				TransactionManager.MaximumTimeout = timeout;
			}
		}
	}
}
