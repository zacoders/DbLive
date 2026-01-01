
namespace DbLive.Tests.Project;

public class ConstructorTests
{
	//[Fact]
	//public void ProjectPathDoesntExists()
	//{
	//	string projectPath = @"C:/DB/";

	//	MockSet mockSet = new();

	//	mockSet.UserProjectPath.Path.Returns(projectPath);
	//	mockSet.FileSystem.PathExistsAndNotEmpty(projectPath).Returns(false);

	//	var projectPathAccessor = new ProjectPathAccessor(mockSet.UserProjectPath, mockSet.FileSystem);

	//	Assert.Throws<ProjectFolderIsEmptyException>(
	//		() => projectPathAccessor.VisualStudioProjectPath
	//	);
	//}

	//[Fact]
	//public void ProjectPathExistsAndNotEmpty()
	//{
	//	string projectPath = @"C:/DB/";

	//	MockSet mockSet = new();

	//	mockSet.ProjectPath.Path.Returns(projectPath);
	//	mockSet.FileSystem.PathExistsAndNotEmpty(projectPath).Returns(true);

	//	var projectPathAccessor = new ProjectPathAccessor(mockSet.ProjectPath, mockSet.FileSystem);

	//	Assert.NotNull(projectPathAccessor.VisualStudioProjectPath);
	//}
}