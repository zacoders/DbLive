namespace DbLive.Tests.Project;

public class GetFolderItemsTests
{
	[Fact]
	public async Task BeforeDeployItemsTest()
	{
		MockSet mockSet = new();

		mockSet.ScriptSource.AddFiles(
			"BeforeDeploy/file2.sql",
			"BeforeDeploy/File1.sql",
			"BeforeDeploy/file3.sql",
			"BeforeDeploy/notes.txt",
			"AfterDeploy/file0.sql"
		);

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		GenericItem[] items = (await sqlProject.GetFolderItemsAsync(ProjectFolder.BeforeDeploy)).ToArray();

		// checking order, this is important.
		Assert.Equal(["File1.sql", "file2.sql", "file3.sql"], items.Select(i => i.Name));
		Assert.Equal("BeforeDeploy/File1.sql", items[0].FileData.RelativePath);
	}

	[Fact]
	public async Task FolderItems_SortedByRelativePath_IncludingSubfolders()
	{
		MockSet mockSet = new();

		mockSet.ScriptSource.AddFiles(
			"AfterDeploy/b/1.sql",
			"AfterDeploy/b.sql",
			"AfterDeploy/a/2.sql",
			"AfterDeploy/A_1.sql"
		);

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		GenericItem[] items = (await sqlProject.GetFolderItemsAsync(ProjectFolder.AfterDeploy)).ToArray();

		// ordinal ignoring case, the same on every OS and in every culture.
		Assert.Equal(
			["AfterDeploy/a/2.sql", "AfterDeploy/A_1.sql", "AfterDeploy/b.sql", "AfterDeploy/b/1.sql"],
			items.Select(i => i.FileData.RelativePath)
		);
	}

	[Fact]
	public async Task AfterDeploy_Empty()
	{
		MockSet mockSet = new();

		mockSet.ScriptSource.AddFile("BeforeDeploy/file1.sql");

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		GenericItem[] items = (await sqlProject.GetFolderItemsAsync(ProjectFolder.AfterDeploy)).ToArray();

		Assert.Empty(items);
	}

	[Fact]
	public async Task GetFolderItems_With_NonExistedFolder()
	{
		MockSet mockSet = new();

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sqlProject.GetFolderItemsAsync((ProjectFolder)999));
	}
}
