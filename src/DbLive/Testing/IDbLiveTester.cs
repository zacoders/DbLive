
namespace DbLive.Testing;

public interface IDbLiveTester
{
	Task<TestRunResult> RunTestAsync(Action<string> writeLine, string relativePath);
}
