namespace EasyFlow.Tests.Project;

public class GetFolderItemsTests
{
	[Fact]
	public void BeforeDeployItemsTest()
	{
		MockSet mockSet = new();

		mockSet.ProjectPathAccessor.ProjectPath.Returns(@"C:\DB\");

		var sqlProject = mockSet.CreateUsingMocks<EasyFlowProject>();

		string folderPath = Path.Combine(@"C:\DB\", "BeforeDeploy");

		mockSet.FileSystem.PathExists(folderPath).Returns(true);

		mockSet.FileSystem.EnumerateFiles(folderPath, "*.sql", true)
			.Returns(
			[
				folderPath.CombineWith("file2.sql"),
				folderPath.CombineWith("file1.sql"),
				folderPath.CombineWith("file3.sql")
			]);

		GenericItem[] items = sqlProject.GetFolderItems(ProjectFolder.BeforeDeploy).ToArray();

		Assert.Equal(3, items.Length);

		// checking order, this is important. 
		Assert.Equal("file1.sql", items[0].Name);
		Assert.Equal("file2.sql", items[1].Name);
		Assert.Equal("file3.sql", items[2].Name);
	}

	[Fact]
	public void AfterDeploy_Empty()
	{
		MockSet mockSet = new();

		mockSet.ProjectPathAccessor.ProjectPath.Returns(@"C:\DB\");

		var sqlProject = mockSet.CreateUsingMocks<EasyFlowProject>();

		string folderPath = Path.Combine(@"C:\DB\", "AfterDeploy");

		mockSet.FileSystem.PathExists(folderPath).Returns(true);

		GenericItem[] items = sqlProject.GetFolderItems(ProjectFolder.AfterDeploy).ToArray();

		Assert.Empty(items);

		mockSet.FileSystem.Received(1).PathExists(folderPath);
		mockSet.FileSystem.Received(1).EnumerateFiles(folderPath, "*.sql", true);
	}

	[Fact]
	public void AfterDeploy_NotImplementedException()
	{
		MockSet mockSet = new();

		mockSet.ProjectPathAccessor.ProjectPath.Returns(@"C:\DB\");

		var sqlProject = mockSet.CreateUsingMocks<EasyFlowProject>();

		Assert.Throws<NotImplementedException>(() => sqlProject.GetFolderItems(ProjectFolder.Unspecified));
	}
}