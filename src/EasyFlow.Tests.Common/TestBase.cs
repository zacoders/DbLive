namespace EasyFlow.Tests.Common;

public class TestBase
{
	protected ITestOutputHelper Output { get; }

	public TestBase(ITestOutputHelper output)
	{
		Output = output;

		Log.Logger = new LoggerConfiguration()
			// add the xunit test output sink to the serilog logger
			// https://github.com/trbenning/serilog-sinks-xunit#serilog-sinks-xunit
			.WriteTo.TestOutput(output)
			.CreateLogger();
	}
}