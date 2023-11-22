using EasyFlow;
using EasyFlow.Common;
using EasyFlow.Project;
using EasyFlow.Tests.Common;
using Xunit.Abstractions;

namespace TestProject1;

public class EasyFlowTesting : IntegrationTestsBase
{
	private static readonly string _msSqlTestingProjectPath = Path.GetFullPath(@"TestProject_MSSQL");

	public EasyFlowTesting(ITestOutputHelper output) : base(output)
	{
		Container.InitializeEasyFlow(DBEngine.MSSQL);
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
		EasyFlowProject p = new(new FileSystem());
		p.Load(_msSqlTestingProjectPath);

		int indexer = 0;
		foreach (var testItem in p.GetTests())
		{
			yield return new object[] { indexer++, testItem.Name };
		}
	}
}