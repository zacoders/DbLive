namespace DbLive.Project.Exceptions;


[ExcludeFromCodeCoverage]
public class BreakingScriptMatchesTestPatternException(string relativePath)
	: Exception($"File '{relativePath}' is named as a breaking script of the Code folder (breaking.sql, b.sql, *.breaking.sql, *.b.sql) and matches TestFilePatterns, so it would be both a breaking script and a test. Rename it.")
{
	public string RelativePath { get; private set; } = relativePath;
}
