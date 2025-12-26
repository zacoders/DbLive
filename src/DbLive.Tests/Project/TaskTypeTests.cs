using DbLive.Project.Exceptions;

namespace DbLive.Tests.Project;

public class TaskTypeTests
{
	[Fact]
	public void GetMigrationType()
	{
		Assert.Equal(MigrationItemType.Migration, DbLiveProject.GetMigrationType("001.migration.sql"));
		Assert.Equal(MigrationItemType.Migration, DbLiveProject.GetMigrationType("001.m.some-notes.sql"));
		Assert.Equal(MigrationItemType.Undo, DbLiveProject.GetMigrationType("001.u.sql"));
		Assert.Equal(MigrationItemType.Undo, DbLiveProject.GetMigrationType("001.undo.with-notes.sql"));
		Assert.Equal(MigrationItemType.Breaking, DbLiveProject.GetMigrationType("001.Breaking.some-breaking-changes-notes.sql"));
		Assert.Equal(MigrationItemType.Breaking, DbLiveProject.GetMigrationType("001.B.sql"));
	}

	[Fact]
	public void GetMigrationType_Unknown()
	{
		Assert.Throws<UnknownMigrationItemTypeException>(() => DbLiveProject.GetMigrationType("test-unknown.sql"));
	}
	
	[Fact]
	public void GetMigrationType_Settings()
	{
		Assert.Equal(MigrationItemType.Settings, DbLiveProject.GetMigrationType("001.settings.json"));
		Assert.Equal(MigrationItemType.Settings, DbLiveProject.GetMigrationType("001.s.json"));
		Assert.Equal(MigrationItemType.Settings, DbLiveProject.GetMigrationType("001.custom-note.json"));
	}
}