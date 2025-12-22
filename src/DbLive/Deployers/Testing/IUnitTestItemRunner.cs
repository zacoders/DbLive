namespace DbLive.Deployers.Testing;

public interface IUnitTestItemRunner
{
	/// <summary>
	/// Runs Sql test.
	/// </summary>
	/// <param name="test"></param>
	/// <param name="settings"></param>
	/// <returns></returns>
	TestRunResult RunTest(TestItem test);
}