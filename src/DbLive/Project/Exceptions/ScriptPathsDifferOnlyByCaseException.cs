namespace DbLive;

/// <summary>
/// Two files of the project have relative paths that differ only by case, possible on Linux. DbLive finds files and identifies them
/// in the database ignoring case. Nothing ran.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class ScriptPathsDifferOnlyByCaseException : Exception
{
	internal ScriptPathsDifferOnlyByCaseException(string relativePath, string otherRelativePath)
		: base($"Files '{relativePath}' and '{otherRelativePath}' differ only by case. DbLive finds files and identifies them in the database ignoring case, so rename or delete one of them.")
	{
		RelativePaths = [relativePath, otherRelativePath];
	}

	/// <summary>
	/// The relative paths of the two files.
	/// </summary>
	public IReadOnlyList<string> RelativePaths { get; private set; }
}
