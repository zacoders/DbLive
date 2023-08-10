namespace EasyFlow.Project.Tests;

[TestClass]
public class TaskTypeTests
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