namespace EasyFlow.Deployers.Testing;

public interface IUnitTestsRunner
{
	void RunAllTests(DeployParameters parameters);

	/// <summary>
	/// Runs Sql test.
	/// </summary>
	/// <param name="test"></param>
	/// <param name="settings"></param>
	/// <returns></returns>
	TestRunResult RunTest(TestItem test);
}