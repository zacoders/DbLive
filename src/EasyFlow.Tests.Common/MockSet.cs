using EasyFlow.Adapter;
using EasyFlow.Common;
using EasyFlow.Deployers.Code;
using EasyFlow.Deployers.Migrations;

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


	public MockSet(bool useDefaultSettings = true)
	{
		if (useDefaultSettings)
		{
			SettingsAccessor.ProjectSettings.Returns(new EasyFlowSettings());
		}

		TransactionRunner.ExecuteWithinTransaction(
			Arg.Any<bool>(), 
			Arg.Any<TranIsolationLevel>(), 
			Arg.Any<TimeSpan>(), 
			Arg.Invoke()
		);
		
		foreach (var fld in GetType().GetFields().Where(fld => !fld.Name.StartsWith("_")))
		{
			// this replaces adding all fields to _container:
			//		_container.AddTransient(f => SettingsAccessor);
			//		_container.AddTransient(f => Logger);
			var val = fld.GetValue(this);
			_container.AddTransient(fld.FieldType, f => val!);
		}
	}

	public TService CreateUsingMocks<TService>() where TService : class
	{		
		_container.AddTransient<TService>();
		var serviceProvider = _container.BuildServiceProvider();
		return serviceProvider.GetService<TService>() 
			?? throw new Exception($"Cannot resolve {typeof(TService).Name}.");
	}
}
