using EasyFlow;
using EasyFlow.Adapter.Interface;
using EasyFlow.Common;
using EasyFlow.Deployers;
using EasyFlow.Project;
using Xunit.Abstractions;

namespace TestProject1;

public abstract class EasyFlowTesting2 : TheoryData<string>, IDisposable
{
	private readonly Dictionary<string, TestItem> TestsList;
	
	private readonly IUnitTestsRunner _unitTestsRunner;
	private readonly IEasyFlowDA _easyFlowDa;
	private readonly IEasyFlow _easyFlow;
	private readonly EasyFlowProject _project;

	public EasyFlowTesting2(
		IUnitTestsRunner unitTestsRunner,
		IEasyFlowDA easyFlowDa,
		IEasyFlow easyFlow,
		EasyFlowProject project
	)
	{
		_unitTestsRunner = unitTestsRunner;
		_project = project;
		_easyFlow = easyFlow;
		_easyFlowDa = easyFlowDa;

		TestsList = _project.GetTests().ToDictionary(i => i.FileData.RelativePath, i => i);

		foreach (var testItem in TestsList)
		{
			Add(testItem.Key); // adding tests to TheoryData base class.
		}

		PrepareTestingDatabase();
	}

	public void Dispose()
	{
		_easyFlowDa.DropDB();
		GC.SuppressFinalize(this);
	}

	protected void PrepareTestingDatabase()
	{		
		DeployParameters deployParams = new()
		{
			CreateDbIfNotExists = true,
			DeployBreaking = true,
			DeployCode = true,
			DeployMigrations = true,
			RunTests = false /* we will run tests in Visual Studio UI */
		};

		_easyFlow.Deploy(deployParams);
	}

	/// <summary>
	/// Runs Sql test.
	/// </summary>
	/// <param name="output">xUnit <see cref="ITestOutputHelper"/></param>
	/// <param name="relativePath">Relative path to the sql test.</param>
	public void RunTest(ITestOutputHelper output, string relativePath)
	{
		output.WriteLine($"Running unit test {relativePath}");
		output.WriteLine("");

		var testItem = TestsList[relativePath];

		output.WriteLine($"{testItem.FileData.Content}");

		var testRunResult = _unitTestsRunner.RunTest(testItem, new EasyFlowSettings());

		output.WriteLine(testRunResult.Output);

		Assert.True(testRunResult.IsSuccess, testRunResult.ErrorMessage);
	}
}
