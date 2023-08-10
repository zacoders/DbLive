using EasyFlow.Adapter;

namespace EasyFlow.Tests;

internal class MockSet
{
	public readonly Mock<IFileSystem> FileSystem = new Mock<IFileSystem>();
	public readonly Mock<IEasyFlowProject> EasyFlowProject = new Mock<IEasyFlowProject>();
	public readonly Mock<IEasyFlowDA> EasyFlowDA = new Mock<IEasyFlowDA>();
}
