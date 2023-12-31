using EasyFlow;
using EasyFlow.Adapter.Interface;
using EasyFlow.Common;
using EasyFlow.Deployers;
using EasyFlow.Project;
using Microsoft.Extensions.DependencyInjection;
using System.Collections;
using Xunit.Abstractions;

namespace TestProject1;

public abstract class EasyFlowTesting : IEnumerable<object[]>, IDisposable
{
	private readonly ServiceProvider _serviceProvider;
	private readonly IUnitTestsRunner _unitTestsRunner;
	private readonly IEasyFlowDA _easyFlowDa;
	private readonly string _projectPath;
	private readonly string _sqlConnectionString;
	private readonly Dictionary<string, TestItem> TestsList;

	public EasyFlowTesting(IServiceCollection container, string projectPath, string sqlConnectionString)
	{
		_projectPath = projectPath;
		_sqlConnectionString = sqlConnectionString;

		container.InitializeEasyFlow();
		_serviceProvider = container.BuildServiceProvider();

		TestsList = GetTests(projectPath);

		_unitTestsRunner = GetService<IUnitTestsRunner>();
		_easyFlowDa = GetService<IEasyFlowDA>();

		PrepareTestingDatabase();
	}

	public void Dispose()
	{
		_easyFlowDa.DropDB(_sqlConnectionString);
		GC.SuppressFinalize(this);
	}

	protected void PrepareTestingDatabase()
	{
		var easyFlow = GetService<IEasyFlow>();

		DeployParameters deployParams = new()
		{
			CreateDbIfNotExists = true,
			DeployBreaking = true,
			DeployCode = true,
			DeployMigrations = true,
			RunTests = false /* we will run tests in Visual Studio UI */
		};

		easyFlow.DeployProject(_projectPath, _sqlConnectionString, deployParams);
	}

	private TService GetService<TService>()
	{
		return _serviceProvider.GetService<TService>() ?? throw new Exception($"Cannot resolve {typeof(TService).Name}.");
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

		var testRunResult = _unitTestsRunner.RunTest(testItem, _sqlConnectionString, new EasyFlowSettings());

		output.WriteLine(testRunResult.Output);

		Assert.True(testRunResult.IsSuccess, testRunResult.ErrorMessage);
	}

	/// <summary>
	/// Returns list of tests.
	/// </summary>
	/// <returns>
	/// An IEnumerable of object arrays, where each array contains a single element representing a test key which is relative path to the sql test (string).
	/// </returns>
	public IEnumerator<object[]> GetEnumerator()
	{
		foreach (var testItem in TestsList)
		{
			yield return new object[] { testItem.Key };
		}
	}

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	private static Dictionary<string, TestItem> GetTests(string projectPath)
	{
		var project = new EasyFlowProject(new FileSystem());
		project.Load(projectPath);

		return project.GetTests().ToDictionary(i => i.FileData.RelativePath, i => i);
	}
}
