using DbLive.Project.Exceptions;

namespace EasyFlow.Tests.Project;

public class ConstructorTests
{
	[Fact]
	public void ProjectPathDoesntExists()
	{
		string projectPath = @"C:\DB\";

		MockSet mockSet = new();

		mockSet.ProjectPathAccessor.ProjectPath.Returns(projectPath);
		mockSet.FileSystem.PathExistsAndNotEmpty(projectPath).Returns(false);

		var projectPathAccessor = new ProjectPathAccessor(new ProjectPath(projectPath), mockSet.FileSystem);

		Assert.Throws<ProjectFolderIsEmptyException>(
			() => projectPathAccessor.ProjectPath
		);
	}

	[Fact]
	public void ProjectPathExistsAndNotEmpty()
	{
		string projectPath = @"C:\DB\";

		MockSet mockSet = new();

		mockSet.ProjectPathAccessor.ProjectPath.Returns(projectPath);
		mockSet.FileSystem.PathExistsAndNotEmpty(projectPath).Returns(true);

		var projectPathAccessor = new ProjectPathAccessor(new ProjectPath(projectPath), mockSet.FileSystem);

		Assert.NotNull(projectPathAccessor.ProjectPath);
	}
}