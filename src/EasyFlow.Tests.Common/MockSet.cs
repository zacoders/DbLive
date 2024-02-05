using EasyFlow.Adapter;
using EasyFlow.Common;

namespace EasyFlow.Tests.Common;

public class MockSet
{
	public readonly ILogger Logger = Substitute.For<ILogger>();
	public readonly IFileSystem FileSystem = Substitute.For<IFileSystem>();
	public readonly IEasyFlowProject EasyFlowProject = Substitute.For<IEasyFlowProject>();
	public readonly IEasyFlowDA EasyFlowDA = Substitute.For<IEasyFlowDA>();
	public readonly IEasyFlowPaths EasyFlowPaths = Substitute.For<IEasyFlowPaths>();
	public readonly ITimeProvider TimeProvider = Substitute.For<ITimeProvider>();
	public readonly IProjectPathAccessor ProjectPathAccessor = Substitute.For<IProjectPathAccessor>();
	public readonly IEasyFlowDbConnection DbConnection = Substitute.For<IEasyFlowDbConnection>();

	public readonly ISettingsAccessor DefaultSettingsAccessor;


	public MockSet()
	{
		DefaultSettingsAccessor = Substitute.For<ISettingsAccessor>();
		DefaultSettingsAccessor.ProjectSettings.Returns(new EasyFlowSettings());
	}
}
