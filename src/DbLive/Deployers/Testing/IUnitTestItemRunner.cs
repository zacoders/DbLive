namespace DbLive.Deployers.Testing;

public interface IUnitTestItemRunner
{
	/// <summary>
	/// Runs Sql test.
	/// </summary>
	/// <param name="test"></param>
	/// <param name="settings"></param>
	/// <returns></returns>
	Task<TestRunResult> RunTestAsync(TestItem test);
}