namespace EasyFlow.Tests.Common;

public class TestBase
{
	public TestBase(ITestOutputHelper output)
	{
		Log.Logger = new LoggerConfiguration()
			// add the xunit test output sink to the serilog logger
			// https://github.com/trbenning/serilog-sinks-xunit#serilog-sinks-xunit
			.WriteTo.TestOutput(output)
			.CreateLogger();
	}
}