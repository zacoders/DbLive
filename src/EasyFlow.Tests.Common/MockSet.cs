using EasyFlow.Adapter;
using EasyFlow.Common;
using EasyFlow.Deployers;
using EasyFlow.Deployers.Code;
using EasyFlow.Deployers.Migrations;
using EasyFlow.Deployers.Testing;

namespace EasyFlow.Tests.Common;

public class MockSet
{
	private readonly IServiceCollection _container = new ServiceCollection();

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
	public readonly IUnitTestItemRunner UnitTestItemRunner = Substitute.For<IUnitTestItemRunner>();
	public readonly ICodeDeployer CodeDeployer = Substitute.For<ICodeDeployer>();
	public readonly IFolderDeployer FolderDeployer = Substitute.For<IFolderDeployer>();
	public readonly IBreakingChangesDeployer BreakingChangesDeployer = Substitute.For<IBreakingChangesDeployer>();
	public readonly IMigrationsDeployer MigrationsDeployer = Substitute.For<IMigrationsDeployer>();
	public readonly IUnitTestsRunner UnitTestsRunner = Substitute.For<IUnitTestsRunner>();
	public readonly IEasyFlowBuilder EasyFlowBuilder = Substitute.For<IEasyFlowBuilder>();
	public readonly IEasyFlowInternal EasyFlowInternal = Substitute.For<IEasyFlowInternal>();
	public readonly IEasyFlowInternalManager SelfDeployer = Substitute.For<IEasyFlowInternalManager>();
	public readonly IUnitTestResultChecker UnitTestResultChecker = Substitute.For<IUnitTestResultChecker>();

	public MockSet()
	{
		SettingsAccessor.ProjectSettings.Returns(new EasyFlowSettings());

		TransactionRunner.ExecuteWithinTransaction(
			Arg.Any<bool>(),
			Arg.Any<TranIsolationLevel>(),
			Arg.Any<TimeSpan>(),
			Arg.Invoke()
		);

		foreach (var fld in GetType().GetFields().Where(fld => !fld.IsPrivate))
		{
			// this replaces adding all fields to _container:
			//		_container.AddTransient(_ => SettingsAccessor);
			//		_container.AddTransient(_ => Logger);
			_container.AddTransient(fld.FieldType, _ => fld.GetValue(this)!);
		}

		// Simplifying testing, ForContext() method returns the same mocked logger.
		Logger.ForContext(Arg.Any<Type>()).Returns(Logger);
	}

	public TService CreateUsingMocks<TService>() where TService : class
	{
		_container.AddTransient<TService>();
		var serviceProvider = _container.BuildServiceProvider();
		return serviceProvider.GetService<TService>()
			?? throw new Exception($"Cannot resolve {typeof(TService).Name}.");
	}
}
