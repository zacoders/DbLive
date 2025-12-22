using DbLive.Project.Exceptions;

namespace DbLive.Tests.Project;

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
			_ = DbLiveProject.GetMigrationType(migrationStr);
		}
	}

	[Fact]
	public void GetMigrationType_Unknow()
	{
		Assert.Throws<UnknowMigrationItemTypeException>(() => DbLiveProject.GetMigrationType("test-unknown"));
	}
}