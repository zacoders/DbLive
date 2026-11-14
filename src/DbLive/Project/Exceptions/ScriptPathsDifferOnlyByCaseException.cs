namespace DbLive;


[ExcludeFromCodeCoverage]
public sealed class ScriptPathsDifferOnlyByCaseException : Exception
{
	internal ScriptPathsDifferOnlyByCaseException(string relativePath, string otherRelativePath)
		: base($"Files '{relativePath}' and '{otherRelativePath}' differ only by case. DbLive finds files and identifies them in the database ignoring case, so rename or delete one of them.")
	{
		RelativePaths = [relativePath, otherRelativePath];
	}

	public IReadOnlyList<string> RelativePaths { get; private set; }
}
