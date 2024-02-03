using EasyFlow.Deployers.Tests;
using System.Collections.ObjectModel;

namespace EasyFlow.Testing;

public interface IEasyFlowTester
{
	TestRunResult RunTest(Action<string> writeLine, string relativePath);
}
