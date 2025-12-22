using MyUnitTestingFramework;
using Xunit.Abstractions;

namespace TestProject1;

public class DBTests
{
	private readonly ITestOutputHelper _output;
	public DBTests(ITestOutputHelper output)
	{
		_output = output;
	}

	[Fact]
	public void Test1()
	{

	}

	[TestCaseFact(DisplayName = "MySqlTest", RootPath = @"C:\Data\Personal\DbLive\prototypes\TestFramewrok\TestProject1")]
	public void SQL(string path)
	{
		_output.WriteLine($"Testing file: {path}");
	}


	[TestCaseFact(RootPath = @"C:\Data\Personal\DbLive\prototypes\TestFramewrok\TestProject1\OtherTests")]
	public void Other(string path)
	{
		_output.WriteLine($"Testing file: {path}");
	}
}