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

	[TestCaseFact]
	public void SQL(string path)
	{
		_output.WriteLine("Testing file: ");
		_output.WriteLine(path);
	}
}