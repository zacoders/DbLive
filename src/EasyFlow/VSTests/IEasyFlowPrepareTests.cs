
namespace EasyFlow.VSTests;

public interface IEasyFlowPrepareTests
{
	TestItem[] TestItems { get; }
	void Load(string projectPath);
	void PrepareUnitTestingDatabase(string sqlConnectionString);
	TestRunResult Run(TestItem testItem, string sqlConnectionString, EasyFlowSettings settings);
}