namespace EasyFlow.Tests.Deployers;

public class FolderDeployerTests
{
	[Fact]
	public void DeployFolder_With_Three_Items()
	{
		var mockSet = new MockSet();

		var projectFolder = ProjectFolder.BeforeDeploy;

		mockSet.EasyFlowProject.GetFolderItems(projectFolder).Returns(new List<GenericItem>{
			GetGenericItem("file1.sql"),
			GetGenericItem("file2.sql"),
			GetGenericItem("file3.sql")
		}.AsReadOnly());

		FolderDeployer deploy = new(mockSet.Logger, mockSet.EasyFlowProject, mockSet.EasyFlowDA, mockSet.TimeProvider);

		deploy.DeployFolder(projectFolder, DeployParameters.Default);

		mockSet.EasyFlowDA.Received(3)
			.ExecuteNonQuery(Arg.Any<string>());

		mockSet.EasyFlowDA.Received(3)
			.MarkItemAsApplied(projectFolder, Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<int>());
	}

	[Fact]
	public void DeployFolder_With_One_Item()
	{
		var mockSet = new MockSet();

		var projectFolder = ProjectFolder.BeforeDeploy;

		mockSet.EasyFlowProject.GetFolderItems(projectFolder).Returns(new List<GenericItem>{
			GetGenericItem("file1.sql"),
		}.AsReadOnly());

		FolderDeployer deploy = new(mockSet.Logger, mockSet.EasyFlowProject, mockSet.EasyFlowDA, mockSet.TimeProvider);

		deploy.DeployFolder(projectFolder, DeployParameters.Default);

		mockSet.EasyFlowDA.Received()
			.ExecuteNonQuery("Content of file1.sql");

		mockSet.EasyFlowDA.Received()
			.MarkItemAsApplied(projectFolder, @"folder\file1.sql", Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<int>());
	}

	private static GenericItem GetGenericItem(string fileName)
	{
		string relativePath = @$"folder\{fileName}";
		return new GenericItem
		{
			Name = fileName,
			FileData = new FileData
			{
				Content = $"Content of {fileName}",
				FilePath = Path.Combine(@"C:\DB", relativePath),
				RelativePath = relativePath
			}
		};
	}

	[Fact]
	public void DeployFolder_EmptyFolder()
	{
		var mockSet = new MockSet();

		var projectFolder = ProjectFolder.BeforeDeploy;

		mockSet.EasyFlowProject.GetFolderItems(projectFolder).Returns(ReadOnlyCollection<GenericItem>.Empty);

		FolderDeployer deploy = new(mockSet.Logger, mockSet.EasyFlowProject, mockSet.EasyFlowDA, mockSet.TimeProvider);

		deploy.DeployFolder(projectFolder, DeployParameters.Default);

		mockSet.EasyFlowDA.DidNotReceive().ExecuteNonQuery(Arg.Any<string>());
	}
}