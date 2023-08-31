namespace EasyFlow.Tests.Common;

public class MockSet
{
	public readonly IFileSystem FileSystem = Substitute.For<IFileSystem>();
	public readonly IEasyFlowProject EasyFlowProject = Substitute.For<IEasyFlowProject>();
	public readonly IEasyFlowDA EasyFlowDA = Substitute.For<IEasyFlowDA>();
	public readonly IEasyFlowDeployer EasyFlowDeployer = Substitute.For<IEasyFlowDeployer>();
	public readonly IEasyFlowPaths EasyFlowPaths = Substitute.For<IEasyFlowPaths>();
}
