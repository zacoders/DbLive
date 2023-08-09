namespace EasySqlFlow;

[Serializable]
internal class MigrationVersionParseException : Exception
{
	public string FolderName { get; private set; }
	public string MigrationVersionStr { get; private set; }

	public MigrationVersionParseException(string folderName, string migrationVersionStr)
		: base($"Cannot parse folder '{folderName}', and version part '{migrationVersionStr}'.")
	{
		FolderName = folderName;
		MigrationVersionStr = migrationVersionStr;
	}
}