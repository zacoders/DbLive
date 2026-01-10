using DbLive.Deployers.Folder;
using System.Threading.Tasks;

namespace DbLive.Tests.Deployers;

public class FolderDeployerTests
{
	[Fact]
	public async Task DeployFolder_With_Three_Items()
	{
		MockSet mockSet = new();

		var projectFolder = ProjectFolder.BeforeDeploy;

		mockSet.DbLiveProject.GetFolderItemsAsync(projectFolder).Returns(new List<GenericItem>{
			GetGenericItem("file1.sql"),
			GetGenericItem("file2.sql"),
			GetGenericItem("file3.sql")
		}.AsReadOnly());

		var deploy = mockSet.CreateUsingMocks<FolderDeployer>();

		await deploy.DeployAsync(projectFolder, DeployParameters.Default);

		mockSet.DbLiveDA.Received(3)
			.ExecuteNonQuery(
				Arg.Any<string>(),
				TranIsolationLevel.ReadCommitted,
				TimeSpan.FromHours(6)
			);

		mockSet.DbLiveDA.Received(3)
			.MarkItemAsApplied(projectFolder, Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<long>());
	}

	[Fact]
	public async Task DeployFolder_With_One_Item()
	{
		MockSet mockSet = new();

		var projectFolder = ProjectFolder.AfterDeploy;

		mockSet.DbLiveProject.GetFolderItemsAsync(projectFolder).Returns(new List<GenericItem>{
			GetGenericItem("file1.sql"),
		}.AsReadOnly());

		var deploy = mockSet.CreateUsingMocks<FolderDeployer>();

		await deploy.DeployAsync(projectFolder, DeployParameters.Default);

		mockSet.DbLiveDA.Received()
			.ExecuteNonQuery(
				"Content of file1.sql",
				TranIsolationLevel.ReadCommitted,
				TimeSpan.FromHours(6)
			);

		mockSet.DbLiveDA.Received()
			.MarkItemAsApplied(projectFolder, @"folder/file1.sql", Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<long>());
	}

	private static GenericItem GetGenericItem(string fileName)
	{
		string relativePath = @$"folder/{fileName}";
		return new GenericItem
		{
			Name = fileName,
			FileData = new FileData
			{
				Content = $"Content of {fileName}",
				FilePath = Path.Combine(@"C:/DB", relativePath),
				RelativePath = relativePath
			}
		};
	}

	[Fact]
	public async Task DeployFolder_EmptyFolder()
	{
		MockSet mockSet = new();

		var projectFolder = ProjectFolder.BeforeDeploy;

		mockSet.DbLiveProject.GetFolderItemsAsync(projectFolder).Returns(ReadOnlyCollection<GenericItem>.Empty);

		var deploy = mockSet.CreateUsingMocks<FolderDeployer>();

		await deploy.DeployAsync(projectFolder, DeployParameters.Default);

		mockSet.DbLiveDA.DidNotReceive().ExecuteNonQuery(Arg.Any<string>());
	}

	[Fact]
	public async Task Deploy_NonExisted_Folder_Type()
	{
		MockSet mockSet = new();

		ProjectFolder projectFolder = (ProjectFolder)999;

		mockSet.DbLiveProject.GetFolderItemsAsync(projectFolder).Returns(new List<GenericItem>{
			GetGenericItem("file1.sql"),
		}.AsReadOnly());

		var deploy = mockSet.CreateUsingMocks<FolderDeployer>();

		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
			() => deploy.DeployAsync(projectFolder, DeployParameters.Default)
		);
	}
}