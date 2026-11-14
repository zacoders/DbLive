namespace DbLive.Tests.Common;

// The tests change the process-wide TransactionManager.MaximumTimeout, so they must not run in parallel with other tests.
[CollectionDefinition(nameof(TransactionScopeManagerTests), DisableParallelization = true)]
public class TransactionScopeManagerTestsCollection;

[Collection(nameof(TransactionScopeManagerTests))]
public class TransactionScopeManagerTests : IDisposable
{
	private readonly TimeSpan _originalMaximumTimeout = TransactionManager.MaximumTimeout;

	public void Dispose()
	{
		TransactionManager.MaximumTimeout = _originalMaximumTimeout;
		GC.SuppressFinalize(this);
	}

	[Fact]
	public async Task Create_TimeoutAboveMaximumTimeout_IsNotCapped()
	{
		TransactionManager.MaximumTimeout = TimeSpan.FromSeconds(1);

		using (TransactionScope scope = TransactionScopeManager.Create(TranIsolationLevel.ReadCommitted, TimeSpan.FromMinutes(1)))
		{
			await Task.Delay(TimeSpan.FromSeconds(3));
			scope.Complete();
		} // throws TransactionAbortedException when the scope timeout is capped at 1 second
	}

	[Fact]
	public void EnsureMaximumTimeout_RaisesTheLimit()
	{
		TransactionManager.MaximumTimeout = TimeSpan.FromMinutes(10);

		TransactionScopeManager.EnsureMaximumTimeout(TimeSpan.FromHours(12));

		Assert.Equal(TimeSpan.FromHours(12), TransactionManager.MaximumTimeout);
	}

	[Fact]
	public void EnsureMaximumTimeout_DoesNotLowerTheLimit()
	{
		TransactionManager.MaximumTimeout = TimeSpan.FromHours(1);

		TransactionScopeManager.EnsureMaximumTimeout(TimeSpan.FromMinutes(1));

		Assert.Equal(TimeSpan.FromHours(1), TransactionManager.MaximumTimeout);
	}

	[Fact]
	public void EnsureMaximumTimeout_ZeroTimeout_RemovesTheLimit()
	{
		TransactionManager.MaximumTimeout = TimeSpan.FromHours(1);

		TransactionScopeManager.EnsureMaximumTimeout(TimeSpan.Zero);

		Assert.Equal(TimeSpan.Zero, TransactionManager.MaximumTimeout);
	}

	[Fact]
	public void EnsureMaximumTimeout_NoLimit_IsKept()
	{
		TransactionManager.MaximumTimeout = TimeSpan.Zero;

		TransactionScopeManager.EnsureMaximumTimeout(TimeSpan.FromHours(12));

		Assert.Equal(TimeSpan.Zero, TransactionManager.MaximumTimeout);
	}
}
