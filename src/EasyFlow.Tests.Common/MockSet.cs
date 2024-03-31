using EasyFlow.Adapter;
using EasyFlow.Common;
using EasyFlow.Deployers.Code;
using EasyFlow.Deployers.Migrations;

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
	public readonly ISettingsAccessor SettingsAccessor = Substitute.For<ISettingsAccessor>();
	public readonly ICodeItemDeployer CodeItemDeployer = Substitute.For<ICodeItemDeployer>();
	public readonly IMigrationDeployer MigrationDeployer = Substitute.For<IMigrationDeployer>();
	public readonly IMigrationItemDeployer MigrationItemDeployer = Substitute.For<IMigrationItemDeployer>();
	public readonly ITransactionRunner TransactionRunner = Substitute.For<ITransactionRunner>();

	public readonly ISettingsAccessor DefaultSettingsAccessor;

	public MockSet()
	{
		DefaultSettingsAccessor = Substitute.For<ISettingsAccessor>();
		DefaultSettingsAccessor.ProjectSettings.Returns(new EasyFlowSettings());
		
		TransactionRunner.ExecuteWithinTransaction(
			Arg.Any<bool>(), 
			Arg.Any<TranIsolationLevel>(), 
			Arg.Any<TimeSpan>(), 
			Arg.Invoke()
		);
	}
}
