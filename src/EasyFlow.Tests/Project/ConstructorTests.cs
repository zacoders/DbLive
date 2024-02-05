using EasyFlow.Project.Exceptions;

namespace EasyFlow.Tests.Project;

public class ConstructorTests
{
	[Fact]
	public void ProjectPathDoesntExists()
	{
		string projectPath = @"C:\DB\";

		var mockSet = new MockSet();

		mockSet.ProjectPath.ProjectPath.Returns(projectPath);
		mockSet.FileSystem.PathExistsAndNotEmpty(projectPath).Returns(false);

		Assert.Throws<ProjectFolderIsEmptyException>(
			() => new EasyFlowProject(mockSet.ProjectPath, mockSet.FileSystem, mockSet.DefaultSettingsAccessor)
		);
	}

	[Fact]
	public void ProjectPathExistsAndNotEmpty()
	{
		string projectPath = @"C:\DB\";

		var mockSet = new MockSet();

		mockSet.ProjectPath.ProjectPath.Returns(projectPath);
		mockSet.FileSystem.PathExistsAndNotEmpty(projectPath).Returns(true);

		_ = new EasyFlowProject(mockSet.ProjectPath, mockSet.FileSystem, mockSet.DefaultSettingsAccessor);
	}
}