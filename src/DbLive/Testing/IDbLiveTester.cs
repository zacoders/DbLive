
namespace DbLive.Testing;

/// <summary>
/// Runs the SQL tests of the project against a deployed database. Created by <see cref="DbLiveBuilder.CreateTester"/>.
/// </summary>
public interface IDbLiveTester
{
	/// <summary>
	/// Runs all SQL tests of the project in parallel and logs the result of each. Writes nothing to the <c>dblive</c> tables.
	/// Not part of a deployment: a production deployer does not call it.
	/// </summary>
	/// <exception cref="UnitTestsFailedException">A test failed. All tests run first.</exception>
	/// <exception cref="DatabaseNotFoundException">The database does not exist.</exception>
	/// <exception cref="DatabaseNotDeployedException">The database has no DbLive tables.</exception>
	/// <exception cref="DatabaseRequiresNewerDbLiveException">A newer DbLive has deployed the database, and this one cannot write its tables.</exception>
	/// <exception cref="ProjectIdMismatchException">The settings have a ProjectId and the database has another one or none.</exception>
	/// <exception cref="NotSupportedTransactionIsolationLevelException">The provider does not support an isolation level of <c>settings.json</c>.</exception>
	Task RunAllTestsAsync();

	/// <summary>
	/// Runs one SQL test, the way <c>[SqlFact]</c> does. Does not check the database and does not save the result.
	/// </summary>
	/// <param name="writeLine">Gets the output of the test: its setup file and what the test returned or why it failed.</param>
	/// <param name="relativePath">The relative path of the test file in the project: <c>Tests/user-name-is-required.sql</c>.
	/// Ignoring case; <c>\</c> is taken as <c>/</c>.</param>
	/// <returns>The result. A failed test is a result, not an exception.</returns>
	/// <exception cref="KeyNotFoundException">The project has no test with <paramref name="relativePath"/>. The message names the path.</exception>
	Task<TestRunResult> RunTestAsync(Action<string> writeLine, string relativePath);
}
