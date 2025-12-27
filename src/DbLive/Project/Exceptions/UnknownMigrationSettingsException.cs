namespace DbLive.Project.Exceptions;


[ExcludeFromCodeCoverage]
public class UnknownMigrationSettingsException(string fileName) 
	: Exception($"Unknown migration settings file {fileName}. Expected ver.setting.json or ver.s.json")
{
}