namespace EasyFlow.Deployers;

public interface IUnitTestsRunner
{
	void RunAllTests(string sqlConnectionString, DeployParameters parameters, EasyFlowSettings settings);

	/// <summary>
	/// Runs Sql test.
	/// </summary>
	/// <param name="test"></param>
	/// <param name="sqlConnectionString"></param>
	/// <param name="settings"></param>
	/// <returns></returns>
	TestRunResult RunTest(TestItem test, string sqlConnectionString, EasyFlowSettings settings);
}