namespace EasyFlow.Project.Tests;

public class TaskTypeTests
{
	[Fact]
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

	[Fact]
	public void GetMigrationType_Unknow()
	{
		Assert.Throws<UnknowMigrationTaskTypeException>(() => EasyFlowProject.GetMigrationType("test-unknown"));
	}
}