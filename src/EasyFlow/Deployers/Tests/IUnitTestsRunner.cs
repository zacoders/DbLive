namespace EasyFlow.Deployers.Tests;

public interface IUnitTestsRunner
{
	void RunAllTests(DeployParameters parameters, EasyFlowSettings settings);

	/// <summary>
	/// Runs Sql test.
	/// </summary>
	/// <param name="test"></param>
	/// <param name="settings"></param>
	/// <returns></returns>
	TestRunResult RunTest(TestItem test, EasyFlowSettings settings);
}