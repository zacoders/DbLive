
namespace DbLive.Tests.Project;

public class TaskTypeTests
{
	[Fact]
	public void GetMigrationType()
	{
		Assert.Equal(MigrationItemType.Migration, DbLiveProject.GetMigrationInfo("001.migration.sql").MigrationItemType);
		Assert.Equal(MigrationItemType.Migration, DbLiveProject.GetMigrationInfo("001.m.some-notes.sql").MigrationItemType);
		Assert.Equal(MigrationItemType.Undo, DbLiveProject.GetMigrationInfo("001.u.sql").MigrationItemType);
		Assert.Equal(MigrationItemType.Undo, DbLiveProject.GetMigrationInfo("001.undo.with-notes.sql").MigrationItemType);
		Assert.Equal(MigrationItemType.Breaking, DbLiveProject.GetMigrationInfo("001.Breaking.some-breaking-changes-notes.sql").MigrationItemType);
		Assert.Equal(MigrationItemType.Breaking, DbLiveProject.GetMigrationInfo("001.B.sql").MigrationItemType);
	}

	[Fact]
	public void GetMigrationType_Unknown_Expected_Migration_For_Unknowns()
	{
		Assert.Equal(MigrationItemType.Migration, DbLiveProject.GetMigrationInfo("003.test-unknown.sql").MigrationItemType);
	}
	
	[Fact]
	public void GetMigrationType_Settings()
	{
		Assert.Equal(MigrationItemType.Settings, DbLiveProject.GetMigrationInfo("001.settings.json").MigrationItemType);
		Assert.Equal(MigrationItemType.Settings, DbLiveProject.GetMigrationInfo("001.s.json").MigrationItemType);
		Assert.Equal(MigrationItemType.Settings, DbLiveProject.GetMigrationInfo("001.custom-note.json").MigrationItemType);
	}
}