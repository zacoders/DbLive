namespace DbLive.Deployers.Folder;

/// <summary>
/// A BeforeDeploy or AfterDeploy script failed. The message names the file and holds the SQL error, or the message of the
/// <see cref="DbLiveSqlException"/> for an error that does not come from the server; the inner exception is the <see cref="DbLiveSqlException"/>.
/// </summary>
[ExcludeFromCodeCoverage]
public class FolderDeploymentException(string relativePath, string error, Exception innerException)
	: Exception($"""
		Error deploying {relativePath} file.
		{error}
		""",
		innerException
	)
{
	public string RelativePath { get; } = relativePath;
}
