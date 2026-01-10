namespace DbLive.Common;

public static class TransactionScopeManager
{
	public static TransactionScope Create(TranIsolationLevel isolationLevel, TimeSpan timeOut)
	{
		TransactionOptions _options = new()
		{
			IsolationLevel = isolationLevel.ToSystemTransaction(),
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
}