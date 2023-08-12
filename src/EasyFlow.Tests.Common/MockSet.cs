namespace EasyFlow.Tests.Common;

public class MockSet
{
	public readonly Mock<IFileSystem> FileSystem = new();
	public readonly Mock<IEasyFlowProject> EasyFlowProject = new();
	public readonly Mock<IEasyFlowDA> EasyFlowDA = new();
	public readonly Mock<IEasyFlowDeployer> EasyFlowDeployer = new();
}
