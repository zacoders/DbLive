namespace DbLive.Project.Exceptions;


[ExcludeFromCodeCoverage]
public class ScriptPathsDifferOnlyByCaseException(string relativePath, string otherRelativePath)
	: Exception($"Files '{relativePath}' and '{otherRelativePath}' differ only by case. DbLive finds files and identifies them in the database ignoring case, so rename or delete one of them.")
{
	public IReadOnlyList<string> RelativePaths { get; private set; } = [relativePath, otherRelativePath];
}
