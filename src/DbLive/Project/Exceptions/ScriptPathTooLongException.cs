namespace DbLive;

/// <summary>
/// The relative path of a file of the project is longer than the <c>dblive</c> tables store for it: on SQL Server the path of a code file,
/// a breaking script of the Code folder and a BeforeDeploy or AfterDeploy file is a key, and a key holds at most 900 bytes. Without
/// the check the file would run and then fail to be recorded. Nothing ran.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class ScriptPathTooLongException : Exception
{
	internal ScriptPathTooLongException(string relativePath, int maxLength, string fileKind)
		: base(
			$"The path of file '{relativePath}' has {relativePath.Length} characters, and the database stores at most {maxLength} for "
			+ $"{fileKind}: on SQL Server the path is a key of a dblive table, and a key holds at most 900 bytes. "
			+ "Shorten the names of its folders or of the file."
		)
	{
		RelativePath = relativePath;
	}

	/// <summary>
	/// The relative path of the file.
	/// </summary>
	public string RelativePath { get; private set; }
}
