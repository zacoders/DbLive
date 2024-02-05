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
			codePath.CombineWith(@"sub\item1.sql"),
			codePath.CombineWith(@"sub\item2.sql")
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
}