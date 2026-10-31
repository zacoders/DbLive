namespace DbLive.Project;

public enum MigrationItemType
{
	Migration,
	Undo,
	Breaking,
	MigrationSettings,
	BreakingSettings,
	UndoSettings
}