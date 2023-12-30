using EasyFlow;
using EasyFlow.Adapter.MSSQL;
using EasyFlow.Common;
using EasyFlow.Tests;
using EasyFlow.VSTests;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace TestProject1;

public class EasyFlowTesting(ITestOutputHelper output) 
	: SqlServerIntegrationBaseTest(output), IDisposable
{
	readonly static string _unitTestsDBName = "EasyFlow-UnitTests-" + nameof(EasyFlowTesting);

	readonly static string _sqlConnectionString = GetDbConnectionString(_unitTestsDBName);

	private static readonly IEasyFlowPrepareTests TestsPrepare;

	static EasyFlowTesting()
	{
		Container.InitializeMSSQL();
		Container.InitializeEasyFlow();
		var serviceProvider = Container.BuildServiceProvider();
		TestsPrepare = serviceProvider.GetService<IEasyFlowPrepareTests>()!;
		TestsPrepare.Load(_msSqlTestingProjectPath);

		TestsPrepare.PrepareUnitTestingDatabase(_sqlConnectionString);
	}

	public void Dispose()
	{
		//DropTestingDatabase(_unitTestsDBName);
	}

	[Theory]
	[MemberData(nameof(GetListOfTests))]
	public void DB(string test, int num)
	{
		Output.WriteLine($"Running unit test #{num}: {test}");

		var testItem = TestsPrepare.TestItems[num];

		var testRunResult = TestsPrepare.Run(testItem, _sqlConnectionString, new EasyFlowSettings());

		Output.WriteLine(testRunResult.Output);

		Assert.True(testRunResult.IsSuccess, testRunResult.ErrorMessage);
	}

	public static IEnumerable<object[]> GetListOfTests()
	{
		//yield return new object[] { "", -1 };

		int indexer = 0;
		foreach (var testItem in TestsPrepare.TestItems)
		{
			yield return new object[] { testItem.Name, indexer++ };
		}
	}
}
