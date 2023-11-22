using EasyFlow;
using EasyFlow.Project;
using EasyFlow.Tests.Common;
using EasyFlow.VSTests;
using Xunit.Abstractions;

namespace TestProject1;

public class EasyFlowTesting : IntegrationTestsBase
{
	private static readonly string _msSqlTestingProjectPath = Path.GetFullPath(@"TestProject_MSSQL");

	private static readonly PrepareTests TestsPrepare = new (DBEngine.MSSQL, _msSqlTestingProjectPath);

	public EasyFlowTesting(ITestOutputHelper output) : base(output)
	{
	}

	[Theory]
	[MemberData(nameof(GetListOfTests))]
	public void RunTests(string testName, int testIndex)
	{
		// Run test for this test item
		//testItem
		Assert.True(testIndex >= 0);
		Output.WriteLine(testName);
	}

	public static IEnumerable<object[]> GetListOfTests()
	{
		//yield return new object[] { 0, "" };

		int indexer = 0;
		foreach (var testItem in TestsPrepare.TestItems)
		{
			yield return new object[] { testItem.Name, indexer++ };
		}
	}
}
