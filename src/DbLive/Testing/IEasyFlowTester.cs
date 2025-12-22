using DbLive.Deployers.Testing;

namespace DbLive.Testing;

public interface IDbLiveTester
{
	TestRunResult RunTest(Action<string> writeLine, string relativePath);
}
