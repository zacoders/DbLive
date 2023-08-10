namespace EasyFlow.Tests;

[TestClass]
public class TaskTypeTests : TestsBase
{
	[TestMethod]
	public void GetMigrationType()
	{
		var testingValues = new[] {
			"migration",
			"UNDO",
			"data",
			"testdata",
			"Breaking"
		};

		foreach (var migrationStr in testingValues)
		{
			_ = EasyFlowProject.GetMigrationType(migrationStr);
		}
	}

	[TestMethod]
	[ExpectedException(typeof(UnknowMigrationTaskTypeException))]
	public void GetMigrationType_Unknow()
	{
		EasyFlowProject.GetMigrationType("test-unknown");
	}
}