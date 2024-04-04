using EasyFlow.Deployers.Testing;

namespace EasyFlow.Testing;

public interface IEasyFlowTester
{
	TestRunResult RunTest(Action<string> writeLine, string relativePath);
}
