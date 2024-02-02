namespace EasyFlow.Tests.Project;

public class CodeItemsTests
{
	[Fact]
	public void GetTests()
	{
		var mockSet = new MockSet();

		mockSet.ProjectPath.ProjectPath.Returns(@"C:\DB\");
		mockSet.FileSystem.PathExistsAndNotEmpty(@"C:\DB\").Returns(true);

		mockSet.FileSystem.PathExists(@"C:\DB\Code").Returns(true);

		mockSet.FileSystem.EnumerateFiles(
			@"C:\DB\Code",
			MyArg.SequenceEqual(["*.sql"]),
			Arg.Any<IEnumerable<string>>(),
			true
		).Returns([
			@"C:\DB\Code\item1.sql",
			@"C:\DB\Code\item2.sql",
			@"C:\DB\Code\sub\item1.sql",
			@"C:\DB\Code\sub\item2.sql"
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