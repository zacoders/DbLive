namespace EasySqlFlow.Tests;

internal class MockSet
{
	public readonly Mock<IFileSystem> FileSystem = new Mock<IFileSystem>();
	public readonly Mock<IEasySqlFlowDA> EasySqlFlowDA = new Mock<IEasySqlFlowDA>();
}
