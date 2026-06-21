namespace DbLive.Project;

public interface IMigrationFileNameParser
{
	MigrationItemInfo GetMigrationInfo(string filePath, bool validateVersion);
}