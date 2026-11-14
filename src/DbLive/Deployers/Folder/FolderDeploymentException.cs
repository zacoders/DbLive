namespace DbLive;

/// <summary>
/// A BeforeDeploy or AfterDeploy script failed. The message names the file and holds the SQL error, or the message of the
/// <see cref="DbLiveSqlException"/> for an error that does not come from the server; the inner exception is the <see cref="DbLiveSqlException"/>.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class FolderDeploymentException : Exception
{
	internal FolderDeploymentException(string relativePath, string error, Exception innerException)
		: base($"""
			Error deploying {relativePath} file.
			{error}
			""",
			innerException
		)
	{
		RelativePath = relativePath;
	}

	public string RelativePath { get; }
}
