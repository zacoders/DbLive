using DbLive.Deployers.Folder;

namespace DbLive.Tests.Deployers;

public class FolderDeployerTests
{
	[Fact]
	public async Task DeployFolder_With_Three_Items()
	{
		MockSet mockSet = new();

		ProjectFolder projectFolder = ProjectFolder.BeforeDeploy;

		ProjectSnapshot project = WithFolderItems(projectFolder, new List<GenericItem>{
			GetGenericItem("file1.sql"),
			GetGenericItem("file2.sql"),
			GetGenericItem("file3.sql")
		}.AsReadOnly());

		FolderDeployer deploy = mockSet.CreateUsingMocks<FolderDeployer>();

		await deploy.DeployAsync(project, projectFolder);

		await mockSet.DbLiveDA.Received(3)
			.ExecuteNonQueryAsync(
				Arg.Any<string>(),
				TranIsolationLevel.ReadCommitted,
				TimeSpan.FromHours(6)
			);

		await mockSet.DbLiveDA.Received(3)
			.MarkItemAsAppliedAsync(projectFolder, Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<long>(), Arg.Any<long>());
	}

	[Fact]
	public async Task DeployFolder_With_One_Item()
	{
		MockSet mockSet = new();

		ProjectFolder projectFolder = ProjectFolder.AfterDeploy;

		ProjectSnapshot project = WithFolderItems(projectFolder, new List<GenericItem>{
			GetGenericItem("file1.sql"),
		}.AsReadOnly());

		FolderDeployer deploy = mockSet.CreateUsingMocks<FolderDeployer>();

		await deploy.DeployAsync(project, projectFolder);

		await mockSet.DbLiveDA.Received()
			.ExecuteNonQueryAsync(
				"Content of file1.sql",
				TranIsolationLevel.ReadCommitted,
				TimeSpan.FromHours(6)
			);

		await mockSet.DbLiveDA.Received()
			.MarkItemAsAppliedAsync(projectFolder, @"folder/file1.sql", Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<long>(), Arg.Any<long>());
	}

	[Fact]
	public async Task Script_error_names_the_file_and_stops_the_folder()
	{
		MockSet mockSet = new();

		ProjectFolder projectFolder = ProjectFolder.AfterDeploy;

		ProjectSnapshot project = WithFolderItems(projectFolder, new List<GenericItem>{
			GetGenericItem("file1.sql"),
			GetGenericItem("file2.sql"),
			GetGenericItem("file3.sql")
		}.AsReadOnly());

		// the error of SQL Server ends with a new line.
		DbLiveSqlException scriptError = new(
			"Invalid object name 'dbo.X'.",
			"Msg 208, Level 16, State 1, Line 1" + Environment.NewLine + "Invalid object name 'dbo.X'." + Environment.NewLine,
			new Exception()
		);

		mockSet.DbLiveDA
			.ExecuteNonQueryAsync("Content of file2.sql", Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan>())
			.Returns(Task.FromException(scriptError));

		FolderDeployer deploy = mockSet.CreateUsingMocks<FolderDeployer>();

		FolderDeploymentException ex = await Assert.ThrowsAsync<FolderDeploymentException>(
			() => deploy.DeployAsync(project, projectFolder)
		);

		Assert.Equal("""
			Error deploying folder/file2.sql file.
			Msg 208, Level 16, State 1, Line 1
			Invalid object name 'dbo.X'.
			""".ReplaceLineEndings(), ex.Message.ReplaceLineEndings());
		Assert.Equal("folder/file2.sql", ex.RelativePath);
		Assert.Same(scriptError, ex.InnerException);

		await mockSet.DbLiveDA.DidNotReceive()
			.ExecuteNonQueryAsync("Content of file3.sql", Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan>());

		await mockSet.DbLiveDA.Received(1)
			.MarkItemAsAppliedAsync(projectFolder, Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<long>(), Arg.Any<long>());
		await mockSet.DbLiveDA.DidNotReceive()
			.MarkItemAsAppliedAsync(projectFolder, "folder/file2.sql", Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<long>(), Arg.Any<long>());
	}

	[Fact]
	public async Task Script_error_without_sql_error_puts_the_exception_message_in_the_message()
	{
		MockSet mockSet = new();

		ProjectFolder projectFolder = ProjectFolder.BeforeDeploy;

		ProjectSnapshot project = WithFolderItems(projectFolder, new List<GenericItem>{
			GetGenericItem("file1.sql"),
		}.AsReadOnly());

		// what PostgreSqlDA throws on a lost connection: no SqlError, the messages of the chain in the message.
		DbLiveSqlException scriptError = new(
			$"Exception while reading from stream{Environment.NewLine}Attempted to read past the end of the stream.",
			"",
			new Exception("Exception while reading from stream", new EndOfStreamException("Attempted to read past the end of the stream."))
		);

		mockSet.DbLiveDA
			.ExecuteNonQueryAsync(Arg.Any<string>(), Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan>())
			.Returns(Task.FromException(scriptError));

		FolderDeployer deploy = mockSet.CreateUsingMocks<FolderDeployer>();

		FolderDeploymentException ex = await Assert.ThrowsAsync<FolderDeploymentException>(
			() => deploy.DeployAsync(project, projectFolder)
		);

		Assert.Equal("""
			Error deploying folder/file1.sql file.
			Exception while reading from stream
			Attempted to read past the end of the stream.
			""".ReplaceLineEndings(), ex.Message.ReplaceLineEndings());
		Assert.Same(scriptError, ex.InnerException);
	}

	[Fact]
	public async Task Failed_write_of_the_row_names_the_file()
	{
		MockSet mockSet = new();

		ProjectFolder projectFolder = ProjectFolder.AfterDeploy;

		ProjectSnapshot project = WithFolderItems(projectFolder, new List<GenericItem>{
			GetGenericItem("file1.sql"),
			GetGenericItem("file2.sql")
		}.AsReadOnly());

		// the write of the row is not a script: its error is the exception of the provider.
		InvalidOperationException writeError = new("The connection is broken.");
		mockSet.DbLiveDA
			.MarkItemAsAppliedAsync(projectFolder, "folder/file1.sql", Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<long>(), Arg.Any<long>())
			.Returns(Task.FromException(writeError));

		FolderDeployer deploy = mockSet.CreateUsingMocks<FolderDeployer>();

		FolderDeploymentException ex = await Assert.ThrowsAsync<FolderDeploymentException>(
			() => deploy.DeployAsync(project, projectFolder)
		);

		Assert.Equal("""
			Error deploying folder/file1.sql file.
			The connection is broken.
			""".ReplaceLineEndings(), ex.Message.ReplaceLineEndings());
		Assert.Equal("folder/file1.sql", ex.RelativePath);
		Assert.Same(writeError, ex.InnerException);
		mockSet.Logger.Received(1).Error(writeError, Arg.Any<string>(), "folder/file1.sql", "The connection is broken.");

		await mockSet.DbLiveDA.DidNotReceive()
			.ExecuteNonQueryAsync("Content of file2.sql", Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan>());
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
				RelativePath = relativePath
			}
		};
	}

	[Fact]
	public async Task DeployFolder_EmptyFolder()
	{
		MockSet mockSet = new();

		ProjectFolder projectFolder = ProjectFolder.BeforeDeploy;

		ProjectSnapshot project = WithFolderItems(projectFolder, ReadOnlyCollection<GenericItem>.Empty);

		FolderDeployer deploy = mockSet.CreateUsingMocks<FolderDeployer>();

		await deploy.DeployAsync(project, projectFolder);

		await mockSet.DbLiveDA.DidNotReceive().ExecuteNonQueryAsync(Arg.Any<string>(), Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan>());
	}

	[Fact]
	public async Task Deploy_NonExisted_Folder_Type()
	{
		MockSet mockSet = new();

		ProjectFolder projectFolder = (ProjectFolder)999;

		ProjectSnapshot project = WithFolderItems(projectFolder, new List<GenericItem>{
			GetGenericItem("file1.sql"),
		}.AsReadOnly());

		FolderDeployer deploy = mockSet.CreateUsingMocks<FolderDeployer>();

		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
			() => deploy.DeployAsync(project, projectFolder)
		);
	}

	private static ProjectSnapshot WithFolderItems(ProjectFolder projectFolder, IReadOnlyList<GenericItem> items) => projectFolder switch
	{
		ProjectFolder.BeforeDeploy => ProjectSnapshots.Empty with { BeforeDeployItems = items },
		ProjectFolder.AfterDeploy => ProjectSnapshots.Empty with { AfterDeployItems = items },
		_ => ProjectSnapshots.Empty
	};
}