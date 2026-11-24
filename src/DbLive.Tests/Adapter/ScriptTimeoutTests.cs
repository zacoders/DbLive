namespace DbLive.Tests.Adapter;

public class ScriptTimeoutTests
{
	[Theory]
	// a timeout of 00:00:00 is no limit, however long the script runs.
	[InlineData("00:00:00", "00:00:00", 0)]
	[InlineData("00:00:00", "01:00:00", 0)]
	[InlineData("00:00:03", "00:00:00", 3)]
	// the time left is rounded up to whole seconds.
	[InlineData("00:00:03", "00:00:02.1", 1)]
	[InlineData("00:00:03", "00:00:02.9999999", 1)]
	[InlineData("00:00:03", "00:00:00.0000001", 3)]
	[InlineData("00:00:01.2", "00:00:00", 2)]
	// a timeout under one second becomes one second, not 0, which would be no limit.
	[InlineData("00:00:00.5", "00:00:00", 1)]
	[InlineData("00:00:00.0000001", "00:00:00", 1)]
	public void Command_timeout_is_the_time_left_rounded_up(string timeout, string elapsed, int expected)
	{
		Assert.Equal(expected, ScriptTimeout.GetCommandTimeoutSeconds(TimeSpan.Parse(timeout), TimeSpan.Parse(elapsed)));
	}

	[Theory]
	[InlineData("00:00:03", "00:00:03")]
	[InlineData("00:00:03", "00:00:03.5")]
	public void No_command_timeout_when_no_time_is_left(string timeout, string elapsed)
	{
		Assert.Null(ScriptTimeout.GetCommandTimeoutSeconds(TimeSpan.Parse(timeout), TimeSpan.Parse(elapsed)));
	}

	[Fact]
	public void Command_timeout_of_a_long_timeout_fits_into_int()
	{
		Assert.Equal(int.MaxValue, ScriptTimeout.GetCommandTimeoutSeconds(TimeSpan.MaxValue, TimeSpan.Zero));
	}

	[Theory]
	[InlineData("00:00:03", "00:00:00", "00:00:03")]
	[InlineData("00:00:03", "00:00:02.1", "00:00:00.9")]
	[InlineData("00:00:00.5", "00:00:00", "00:00:00.5")]
	// no time left: cancel at once.
	[InlineData("00:00:03", "00:00:03", "00:00:00")]
	[InlineData("00:00:03", "00:00:03.5", "00:00:00")]
	public void Cancel_delay_is_the_time_left(string timeout, string elapsed, string expected)
	{
		Assert.Equal(TimeSpan.Parse(expected), ScriptTimeout.GetCancelDelay(TimeSpan.Parse(timeout), TimeSpan.Parse(elapsed)));
	}

	[Theory]
	// a timeout of 00:00:00 is no limit.
	[InlineData("00:00:00", "00:00:00")]
	[InlineData("00:00:00", "01:00:00")]
	// longer than the timer of .NET takes: "Timeout": "60.00:00:00" of the item settings.
	[InlineData("60.00:00:00", "00:00:00")]
	public void No_cancel_delay_for_no_limit(string timeout, string elapsed)
	{
		Assert.Null(ScriptTimeout.GetCancelDelay(TimeSpan.Parse(timeout), TimeSpan.Parse(elapsed)));
	}

	[Fact]
	public void Max_cancel_delay_is_the_longest_that_CancelAfter_takes()
	{
		using CancellationTokenSource cancellation = new();

		cancellation.CancelAfter(ScriptTimeout.MaxCancelDelay);
		_ = Assert.Throws<ArgumentOutOfRangeException>(() => cancellation.CancelAfter(ScriptTimeout.MaxCancelDelay + TimeSpan.FromMilliseconds(1)));
	}

	[Fact]
	public void Cancellation_of_a_timeout_of_60_days_is_created()
	{
		using CancellationTokenSource cancellation = ScriptTimeout.Start(TimeSpan.FromDays(60)).CreateCancellation();

		Assert.False(cancellation.IsCancellationRequested);
	}

	[Fact]
	public async Task Cancellation_is_cancelled_when_the_time_of_the_script_runs_out()
	{
		using CancellationTokenSource cancellation = ScriptTimeout.Start(TimeSpan.FromMilliseconds(50)).CreateCancellation();

		_ = await Assert.ThrowsAsync<TaskCanceledException>(() => Task.Delay(Timeout.Infinite, cancellation.Token));
	}

	[Fact]
	public void Script_that_just_started_gets_its_whole_timeout()
	{
		ScriptTimeout scriptTimeout = ScriptTimeout.Start(TimeSpan.FromSeconds(30));

		Assert.Equal(TimeSpan.FromSeconds(30), scriptTimeout.Timeout);
		Assert.Equal(30, scriptTimeout.GetCommandTimeoutSeconds());
	}

	[Fact]
	public void Time_that_ran_out_between_two_commands_is_the_timeout_of_the_script()
	{
		ScriptTimeout scriptTimeout = new(TimeSpan.FromSeconds(3), () => TimeSpan.FromSeconds(3.2));

		ExecutionTimeoutException ex = Assert.Throws<ExecutionTimeoutException>(() => scriptTimeout.GetCommandTimeoutSeconds());

		Assert.Equal(TimeSpan.FromSeconds(3), ex.Timeout);
		Assert.Equal("Execution Timeout Expired. The script ran longer than its timeout of 00:00:03.", ex.Message);
		Assert.Equal("", ex.SqlError);
		Assert.Null(ex.InnerException);
	}

	[Fact]
	public void Timeout_error_of_the_provider_is_the_timeout_of_the_script()
	{
		ScriptTimeout scriptTimeout = new(TimeSpan.FromSeconds(3), () => TimeSpan.FromSeconds(3.2));
		TimeoutException providerException = new("Timeout during reading attempt");

		ExecutionTimeoutException ex = scriptTimeout.Expired(providerException);

		Assert.Equal(TimeSpan.FromSeconds(3), ex.Timeout);
		Assert.Equal("Execution Timeout Expired. The script ran longer than its timeout of 00:00:03.", ex.Message);
		Assert.Same(providerException, ex.InnerException);
	}
}
