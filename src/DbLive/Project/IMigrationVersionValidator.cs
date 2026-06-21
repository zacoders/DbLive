namespace DbLive.Project;

public interface IMigrationVersionValidator
{
	void Validate(long versionPrefix, string fileName);
}