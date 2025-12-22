using DbLive.Project.Exceptions;

namespace EasyFlow.Tests.Project;

public class TaskTypeTests
{
	[Fact]
	public void GetMigrationType()
	{
		var testingValues = new[] {
			"migration",
			"UNDO",
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
		Assert.Throws<UnknowMigrationItemTypeException>(() => EasyFlowProject.GetMigrationType("test-unknown"));
	}
}