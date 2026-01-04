namespace DbLive.Adapter;

public enum MigrationItemStatus
{
	None,
	Skipped,
	Applied,
	Failed,
	Reverted
}