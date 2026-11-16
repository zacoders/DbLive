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
