namespace DbLive;


[ExcludeFromCodeCoverage]
public sealed class BreakingScriptMatchesTestPatternException : Exception
{
	internal BreakingScriptMatchesTestPatternException(string relativePath)
		: base($"File '{relativePath}' is named as a breaking script of the Code folder (breaking.sql, b.sql, *.breaking.sql, *.b.sql) and matches TestFilePatterns, so it would be both a breaking script and a test. Rename it.")
	{
		RelativePath = relativePath;
	}

	public string RelativePath { get; private set; }
}
