
namespace DbLive.Testing;

public interface IDbLiveTester
{
	/// <summary>
	/// Runs all SQL tests of the project in parallel and saves their results to <c>dblive.unit_test_run</c>.
	/// Not part of a deployment: a production deployer does not call it.
	/// </summary>
	/// <exception cref="UnitTestsFailedException">A test failed. All tests run first.</exception>
	/// <exception cref="DatabaseNotDeployedException">The database has no DbLive tables.</exception>
	/// <exception cref="SelfDeployer.DatabaseRequiresNewerDbLiveException">A newer DbLive has deployed the database, and this one cannot write its tables.</exception>
	/// <exception cref="ProjectIdMismatchException">The settings have a ProjectId and the database has another one or none.</exception>
	Task RunAllTestsAsync();

	Task<TestRunResult> RunTestAsync(Action<string> writeLine, string relativePath);
}
