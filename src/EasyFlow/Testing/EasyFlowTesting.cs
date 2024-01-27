//using EasyFlow.Adapter;
//using System.Collections.ObjectModel;

//namespace EasyFlow.Testing;

//public class EasyFlowTesting : IDisposable
//{
//	private readonly ServiceProvider _serviceProvider;
//	private readonly IUnitTestsRunner _unitTestsRunner;
//	private readonly IEasyFlowDA _easyFlowDa;
//	public readonly ReadOnlyDictionary<string, TestItem> TestsList;
//	protected readonly EasyFlowBuilder _easyFlowBuilder;

//	public EasyFlowTesting(EasyFlowBuilder easyFlowBuilder)
//	{
//		easyFlowBuilder.SetProjectPath(projectPath);
//		easyFlowBuilder.LogToConsole(); //todo: this will not log, since we xunit is used.

//		//var container = easyFlowBuilder.Container;

//		//container.InitializeEasyFlow();
//		//container.SetProjectPath(projectPath);
//		//container.LogToConsole(); 

//		//_serviceProvider = container.BuildServiceProvider();

//		_unitTestsRunner = GetService<IUnitTestsRunner>();

//		_easyFlowDa = GetService<IEasyFlowDA>();

//		var project = GetService<IEasyFlowProject>();

//		TestsList = new ReadOnlyDictionary<string, TestItem>(
//			project.GetTests().ToDictionary(i => i.FileData.RelativePath, i => i)
//		);

//		PrepareTestingDatabase();
//	}

//	public void Dispose()
//	{
//		_easyFlowDa.DropDB();
//		GC.SuppressFinalize(this);
//	}

//	protected void PrepareTestingDatabase()
//	{
//		var easyFlow = GetService<IEasyFlow>();

//		DeployParameters deployParams = new()
//		{
//			CreateDbIfNotExists = true,
//			DeployBreaking = true,
//			DeployCode = true,
//			DeployMigrations = true,
//			RunTests = false /* we will run tests in Visual Studio UI */
//		};

//		easyFlow.Deploy(deployParams);
//	}

//	private TService GetService<TService>()
//	{
//		return _serviceProvider.GetService<TService>() ?? throw new Exception($"Cannot resolve {typeof(TService).Name}.");
//	}

//	/// <summary>
//	/// Runs Sql test.
//	/// </summary>
//	/// <param name="output"><see cref="ITestOutput"/></param>
//	/// <param name="relativePath">Relative path to the sql test.</param>
//	public TestRunResult RunTest(Action<string> writeLine, string relativePath)
//	{
//		writeLine($"Running unit test {relativePath}");
//		writeLine("");

//		var testItem = TestsList[relativePath];

//		writeLine($"{testItem.FileData.Content}");

//		var testRunResult = _unitTestsRunner.RunTest(testItem, new EasyFlowSettings());

//		writeLine(testRunResult.Output);

//		return testRunResult;
//	}
//}
