namespace DbLive.Deployers.Testing;

internal interface IUnitTestItemRunner
{
	/// <summary>
	/// Runs one SQL test, after its <c>setup.test.sql</c>, in a transaction that is rolled back at the end.
	/// </summary>
	/// <param name="test">The test and its setup file.</param>
	/// <returns>The result. A failed test, also an error of its SQL, is a result, not an exception.</returns>
	Task<TestRunResult> RunTestAsync(TestItem test);
}