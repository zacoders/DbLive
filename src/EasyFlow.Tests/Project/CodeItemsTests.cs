namespace EasyFlow.Tests.Project;

public class CodeItemsTests
{
	[Fact]
	public void GetTests()
	{
		var mockSet = new MockSet();

		string projectPath = @"C:\DB";
		mockSet.ProjectPath.ProjectPath.Returns(projectPath);
		mockSet.FileSystem.PathExistsAndNotEmpty(projectPath).Returns(true);

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

		var sqlProject = new EasyFlowProject(mockSet.ProjectPath, mockSet.FileSystem);

		var tests = sqlProject.GetCodeItems().ToList();

		Assert.NotNull(tests);
		Assert.Equal(4, tests.Count);
	}
}