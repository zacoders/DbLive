namespace DbLive.Project;

internal interface IMigrationVersionValidator
{
	void Validate(long versionPrefix, string fileName);
}