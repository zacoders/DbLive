using System.Diagnostics;

namespace DbLive.Tests.Common;

/// <summary>
/// When the timeout of a transaction runs out, System.Transactions aborts it on its timer thread, and the error comes from the next step
/// in the scope. The runner makes it <see cref="ExecutionTimeoutException"/> with that error inside.
/// </summary>
public class TransactionRunnerTests
{
	private static readonly TransactionTimeout ShortTimeout = new(TimeSpan.FromMilliseconds(200), "The deployment", "DeploymentTimeout");

	private readonly TransactionRunner _runner = new();

	[Fact]
	public async Task Error_of_a_step_after_the_transaction_timed_out_is_the_timeout_of_the_transaction()
	{
		InvalidOperationException stepError = new("The transaction associated with the current connection has completed but has not been disposed.");

		ExecutionTimeoutException ex = await Assert.ThrowsAsync<ExecutionTimeoutException>(
			() => RunAsync(ShortTimeout, async () =>
			{
				await WaitUntilAbortedAsync();
				throw stepError;
			})
		);

		Assert.Equal("Execution Timeout Expired. The deployment ran longer than its timeout of 00:00:00.2000000 (DeploymentTimeout).", ex.Message);
		Assert.Equal(TimeSpan.FromMilliseconds(200), ex.Timeout);
		Assert.Equal("", ex.SqlError);
		Assert.Same(stepError, ex.InnerException);
	}

	[Fact]
	public async Task Transaction_that_timed_out_after_its_last_step_is_the_timeout_of_the_transaction()
	{
		ExecutionTimeoutException ex = await Assert.ThrowsAsync<ExecutionTimeoutException>(
			() => RunAsync(new TransactionTimeout(TimeSpan.FromMilliseconds(200), "The self-deployment of DbLive"), WaitUntilAbortedAsync)
		);

		Assert.Equal("Execution Timeout Expired. The self-deployment of DbLive ran longer than its timeout of 00:00:00.2000000.", ex.Message);
		_ = Assert.IsType<TransactionAbortedException>(ex.InnerException);
	}

	[Fact]
	public async Task Timeout_of_a_script_stays_the_timeout_of_the_script()
	{
		MigrationDeploymentException fileError = new("Error deploying 001.migration.sql file.", new ExecutionTimeoutException(TimeSpan.FromMilliseconds(200)));

		MigrationDeploymentException ex = await Assert.ThrowsAsync<MigrationDeploymentException>(
			() => RunAsync(ShortTimeout, async () =>
			{
				await WaitUntilAbortedAsync();
				throw fileError;
			})
		);

		Assert.Same(fileError, ex);
	}

	[Fact]
	public async Task Error_in_a_transaction_that_did_not_time_out_stays_as_it_is()
	{
		InvalidOperationException stepError = new("step failed");

		InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
			() => RunAsync(new TransactionTimeout(TimeSpan.FromMinutes(1), "The deployment", "DeploymentTimeout"), () => throw stepError)
		);

		Assert.Same(stepError, ex);
	}

	[Fact]
	public async Task Transaction_of_the_application_that_ends_by_its_own_timeout_is_not_the_timeout_of_DbLive()
	{
		// the scope of DbLive joins the transaction of the application, whose timeout is shorter.
		using TransactionScope applicationScope = TransactionScopeManager.Create(TranIsolationLevel.ReadCommitted, TimeSpan.FromMilliseconds(200));
		InvalidOperationException stepError = new("step failed");

		InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
			() => RunAsync(new TransactionTimeout(TimeSpan.FromMinutes(1), "The deployment", "DeploymentTimeout"), async () =>
			{
				await WaitUntilAbortedAsync();
				throw stepError;
			})
		);

		Assert.Same(stepError, ex);
	}

	private Task RunAsync(TransactionTimeout timeout, Func<Task> action)
		=> _runner.ExecuteWithinTransactionAsync(true, TranIsolationLevel.ReadCommitted, timeout, action);

	/// <summary>
	/// Waits until System.Transactions aborts the ambient transaction at its timeout: on its timer thread, up to about a second late.
	/// </summary>
	internal static async Task WaitUntilAbortedAsync()
	{
		Transaction transaction = Transaction.Current!;
		Stopwatch stopwatch = Stopwatch.StartNew();

		while (transaction.TransactionInformation.Status != TransactionStatus.Aborted)
		{
			Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), "The transaction did not time out.");
			await Task.Delay(20);
		}
	}
}
