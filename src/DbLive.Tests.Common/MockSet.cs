
using DbLive.Deployers;
using DbLive.Deployers.Folder;
using DbLive.SelfDeployer;
using System.Reflection;

namespace DbLive.Tests.Common;

public class MockSet
{
	private readonly IServiceCollection _container = new ServiceCollection();

	public readonly ILogger Logger = Substitute.For<ILogger>();
	public readonly IFileSystem FileSystem = Substitute.For<IFileSystem>();
	public readonly IDbLiveProject DbLiveProject = Substitute.For<IDbLiveProject>();
	public readonly IInternalDbLiveProject IInternalDbLiveProject = Substitute.For<IInternalDbLiveProject>();
	public readonly IDbLiveDA DbLiveDA = Substitute.For<IDbLiveDA>();
	public readonly IInternalProjectPath InternalProjectPath = Substitute.For<IInternalProjectPath>();
	public readonly ITimeProvider TimeProvider = Substitute.For<ITimeProvider>();
	public readonly IVsProjectPathAccessor VsProjectPathAccessor = Substitute.For<IVsProjectPathAccessor>();
	public readonly IProjectPath ProjectPath = Substitute.For<IProjectPath>();
	public readonly IDbLiveDbConnection DbConnection = Substitute.For<IDbLiveDbConnection>();
	public readonly ISettingsAccessor SettingsAccessor = Substitute.For<ISettingsAccessor>();
	public readonly ICodeItemDeployer CodeItemDeployer = Substitute.For<ICodeItemDeployer>();
	public readonly IMigrationVersionDeployer MigrationVersionDeployer = Substitute.For<IMigrationVersionDeployer>();
	public readonly IMigrationItemDeployer MigrationItemDeployer = Substitute.For<IMigrationItemDeployer>();
	public readonly ITransactionRunner TransactionRunner = Substitute.For<ITransactionRunner>();
	public readonly IUnitTestItemRunner UnitTestItemRunner = Substitute.For<IUnitTestItemRunner>();
	public readonly ICodeDeployer CodeDeployer = Substitute.For<ICodeDeployer>();
	public readonly IFolderDeployer FolderDeployer = Substitute.For<IFolderDeployer>();
	public readonly IDowngradeDeployer DowngradeDeployer = Substitute.For<IDowngradeDeployer>();
	public readonly IBreakingChangesDeployer BreakingChangesDeployer = Substitute.For<IBreakingChangesDeployer>();
	public readonly IMigrationsDeployer MigrationsDeployer = Substitute.For<IMigrationsDeployer>();
	public readonly IMigrationsSaver MigrationsSaver = Substitute.For<IMigrationsSaver>();
	public readonly IUnitTestsRunner UnitTestsRunner = Substitute.For<IUnitTestsRunner>();
	public readonly IDbLiveDeployer DbLiveInternalDeployer = Substitute.For<IDbLiveDeployer>();
	public readonly IDbLiveSelfDeployer DbLiveSelfDeployer = Substitute.For<IDbLiveSelfDeployer>();
	public readonly IUnitTestResultChecker UnitTestResultChecker = Substitute.For<IUnitTestResultChecker>();

	public MockSet()
	{
		SettingsAccessor.GetProjectSettingsAsync().Returns(Task.FromResult(new DbLiveSettings()));

		TransactionRunner
			.ExecuteWithinTransactionAsync(
				Arg.Any<bool>(),
				Arg.Any<TranIsolationLevel>(),
				Arg.Any<TimeSpan>(),
				Arg.Any<Func<Task>>()
			)
			.Returns(ci => ci.Arg<Func<Task>>()());

		foreach (FieldInfo? fld in GetType().GetFields().Where(fld => !fld.IsPrivate))
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
		ServiceProvider serviceProvider = _container.BuildServiceProvider();
		return serviceProvider.GetService<TService>()
			?? throw new Exception($"Cannot resolve {typeof(TService).Name}.");
	}
}
