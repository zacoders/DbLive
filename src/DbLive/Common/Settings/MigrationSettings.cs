namespace DbLive.Common.Settings;

public class MigrationSettings
{
	public TransactionWrapLevel? TransactionWrapLevel { get; init; }

	public TranIsolationLevel? TransactionIsolationLevel { get; init; }

	public TimeSpan? MigrationTimeout { get; init; }
}
