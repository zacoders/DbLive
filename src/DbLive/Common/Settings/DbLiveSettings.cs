namespace DbLive.Common.Settings;

public class DbLiveSettings
{
	public TransactionWrapLevel TransactionWrapLevel { get; init; } = TransactionWrapLevel.Migration;

	public TranIsolationLevel TransactionIsolationLevel { get; init; } = TranIsolationLevel.ReadCommitted;

	public List<string> TestFilePatterns { get; init; } = ["test.*.sql", "t.*.sql", "*.test.sql", "*.t.sql"];

	public TranIsolationLevel TestsTransactionIsolationLevel { get; init; } = TranIsolationLevel.Serializable;

	public string BeforeDeployFolder { get; init; } = "BeforeDeploy";
	public string AfterDeployFolder { get; init; } = "AfterDeploy";

	public string MigrationsFolder { get; init; } = "Migrations";
	public string CodeFolder { get; init; } = "Code";

	public IEnumerable<string> CodeSubFoldersDeploymentOrder { get; init; } = [];

	public string TestsFolder { get; init; } = "Tests";

	public TimeSpan DeploymentTimeout { get; init; } = TimeSpan.FromDays(1);
	public TimeSpan MigrationTimeout { get; init; } = TimeSpan.FromHours(12);
	public TimeSpan BeforeDeployFolderTimeout { get; init; } = TimeSpan.FromHours(6);
	public TimeSpan AfterDeployFolderTimeout { get; init; } = TimeSpan.FromHours(6);
	public TimeSpan CodeItemTimeout { get; init; } = TimeSpan.FromSeconds(30);
	public TimeSpan UnitTestItemTimeout { get; init; } = TimeSpan.FromMinutes(1);
}
