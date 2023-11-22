using EasyFlow;
using EasyFlow.Project;
using EasyFlow.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace TestProject1;

public class EasyFlowTesting : IntegrationTestsBase
{
	private static readonly string _msSqlTestingProjectPath = Path.GetFullPath(@"TestProject_MSSQL");
	private static IReadOnlyCollection<TestItem> ListOfTests { get; set; } = new List<TestItem>();

	public EasyFlowTesting(ITestOutputHelper output) : base(output)
	{
	}

	[Theory]
	[MemberData(nameof(GetListOfTests))]
	public void RunTests(int itemIndex, string testName)
	{
		// Run test for this test item
		//testItem
		Assert.True(itemIndex >= 0);
		Output.WriteLine(testName);
	}

	public static IEnumerable<object[]> GetListOfTests()
	{
		//yield return new object[] { 0, "" };
		var testPrepare = new PrepareTests(DBEngine.MSSQL, _msSqlTestingProjectPath);
		int indexer = 0;
		foreach (var testItem in testPrepare.TestItems)
		{
			yield return new object[] { indexer++, testItem.Name };
		}
	}
}

public class PrepareTests
{
	private IServiceCollection Container { get; }
	public IReadOnlyCollection<TestItem> TestItems { get; }

	public PrepareTests(DBEngine dBEngine, string projectPath)
	{
		Container = new ServiceCollection();
		Container.InitializeEasyFlow(dBEngine);
		var serviceProvider = Container.BuildServiceProvider();
		var project = serviceProvider.GetService<IEasyFlowProject>()!;
		project.Load(projectPath);
		TestItems = project.GetTests();
	}
}
