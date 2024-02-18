namespace EasyFlow.Tests.Project;

public class CodeItemsTests
{
	[Fact]
	public void GetCodeItems()
	{
		var mockSet = new MockSet();

		string projectPath = @"C:\DB";
		mockSet.ProjectPathAccessor.ProjectPath.Returns(projectPath);
		
		string codePath = projectPath.CombineWith("Code");
		mockSet.FileSystem.PathExists(codePath).Returns(true);

		mockSet.FileSystem.EnumerateFiles(
			codePath,
			MyArg.SequenceEqual(["*.sql"]),
			Arg.Any<IEnumerable<string>>(),
			true
		).Returns([
			codePath.CombineWith(@"item1.sql"),
			codePath.CombineWith(@"item2.sql"),
			codePath.CombineWith(@"sub").CombineWith("item1.sql"),
			codePath.CombineWith(@"sub").CombineWith("item2.sql")
		]);

		mockSet.FileSystem.ReadFileData(Arg.Any<string>(), Arg.Any<string>())
			.ReturnsForAnyArgs(call =>
			new FileData
			{
				FilePath = call.Args()[0].ToString()!,
				Content = "code item content",
				RelativePath = ""
			});

		var sqlProject = new EasyFlowProject(mockSet.ProjectPathAccessor, mockSet.FileSystem, mockSet.DefaultSettingsAccessor);

		var codeGroups = sqlProject.GetCodeGroups().ToList();

		Assert.NotNull(codeGroups);
		Assert.Single(codeGroups);
		Assert.Equal(4, codeGroups[0].CodeItems.Count);
	}	
	
	
	[Fact]
	public void GetCodeItems_With_CodeSubFoldersDeploymentOrder()
	{
		var mockSet = new MockSet();

		mockSet.SettingsAccessor.ProjectSettings.Returns(new EasyFlowSettings
		{
			CodeSubFoldersDeploymentOrder = ["sub2", "sub1"]
		});

		string projekt = @"C:\DB";
		string projektPath = projekt;
		mockSet.ProjectPathAccessor.ProjectPath.Returns(projektPath);
		
		string codePath = projektPath.CombineWith("Code");
		mockSet.FileSystem.PathExists(codePath).Returns(true);

		mockSet.FileSystem.EnumerateFiles(
			codePath,
			MyArg.SequenceEqual(["*.sql"]),
			Arg.Any<IEnumerable<string>>(),
			true
		).Returns([
			codePath.CombineWith(@"item1.sql"),
			codePath.CombineWith(@"item2.sql"),
			codePath.CombineWith(@"sub1").CombineWith("item1.sql"),
			codePath.CombineWith(@"sub1").CombineWith("item2.sql"),
			codePath.CombineWith(@"sub1").CombineWith("item3.sql"),
			codePath.CombineWith(@"sub2").CombineWith("item1.sql")
		]);

		mockSet.FileSystem.ReadFileData(Arg.Any<string>(), Arg.Any<string>())
			.ReturnsForAnyArgs(call =>
			new FileData
			{
				FilePath = call.Args()[0].ToString()!,
				Content = "code item content",
				RelativePath = ""
			});

		var sqlProject = new EasyFlowProject(mockSet.ProjectPathAccessor, mockSet.FileSystem, mockSet.SettingsAccessor);

		var codeGroups = sqlProject.GetCodeGroups().ToList();

		Assert.NotNull(codeGroups);
		Assert.Equal(3, codeGroups.Count);

		Assert.Contains("sub2", codeGroups[0].Path);
		Assert.Single(codeGroups[0].CodeItems);

		Assert.Contains("sub1", codeGroups[1].Path);
		Assert.Equal(3, codeGroups[1].CodeItems.Count);
		
		// other items
		Assert.Equal(2, codeGroups[2].CodeItems.Count);
	}
}