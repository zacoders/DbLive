namespace EasyFlow.Deployers
{
	public interface IUnitTestsRunner
	{
		void RunAllTests(string sqlConnectionString, DeployParameters parameters, EasyFlowSettings settings);
		TestRunResult RunTest(TestItem test, string sqlConnectionString, EasyFlowSettings settings);
	}
}