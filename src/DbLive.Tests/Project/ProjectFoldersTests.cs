namespace DbLive.Tests.Project;

public class ProjectFoldersTests
{
	[Theory]
	[InlineData(@"db\{0}")]
	[InlineData("db/{0}")]
	[InlineData("/db/{0}/")]
	[InlineData(@"\db\{0}\")]
	public async Task Nested_Folder_Settings_Work_With_Any_Separator(string folderFormat)
	{
		MockSet mockSet = new();

		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			MigrationsFolder = string.Format(folderFormat, "Migrations"),
			CodeFolder = string.Format(folderFormat, "Code"),
			TestsFolder = string.Format(folderFormat, "Tests"),
			BeforeDeployFolder = string.Format(folderFormat, "BeforeDeploy"),
			AfterDeployFolder = string.Format(folderFormat, "AfterDeploy")
		});

		mockSet.ScriptSource.AddFiles(
			"db/Migrations/001.migration.sql",
			"db/Code/v1.sql",
			"db/Tests/t1.sql",
			"db/BeforeDeploy/before.sql",
			"db/AfterDeploy/after.sql",
			// the same folders in the root are not part of the project.
			"Migrations/002.migration.sql",
			"Code/v2.sql",
			"Tests/t2.sql",
			"BeforeDeploy/before2.sql",
			"AfterDeploy/after2.sql"
		);

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		Migration migration = Assert.Single(await sqlProject.GetMigrationsAsync());
		Assert.Equal(1, migration.Version);

		CodeGroup codeGroup = Assert.Single(await sqlProject.GetCodeGroupsAsync());
		Assert.Equal("db/Code", codeGroup.Path);
		Assert.Equal("db/Code/v1.sql", Assert.Single(codeGroup.CodeItems).FileData.RelativePath);

		Assert.Equal("db/Tests/t1.sql", Assert.Single(await sqlProject.GetTestsAsync()).FileData.RelativePath);
		Assert.Equal("before.sql", Assert.Single(await sqlProject.GetFolderItemsAsync(ProjectFolder.BeforeDeploy)).Name);
		Assert.Equal("after.sql", Assert.Single(await sqlProject.GetFolderItemsAsync(ProjectFolder.AfterDeploy)).Name);
	}

	[Theory]
	[InlineData("")]
	[InlineData("/")]
	public async Task Root_Folder_Contains_All_Files(string codeFolder)
	{
		MockSet mockSet = new();

		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			CodeFolder = codeFolder
		});

		mockSet.ScriptSource.AddFiles("v1.sql", "dbo/v2.sql");

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		CodeGroup codeGroup = Assert.Single(await sqlProject.GetCodeGroupsAsync());
		Assert.Equal("", codeGroup.Path);
		Assert.Equal(["dbo/v2.sql", "v1.sql"], codeGroup.CodeItems.Select(i => i.FileData.RelativePath));
	}
}
