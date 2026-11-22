namespace DbLive;

/// <summary>
/// A file of the Code folder is named as a breaking script and matches <c>TestFilePatterns</c>, such as <c>test.breaking.sql</c>:
/// it would be both a breaking script and a test. Nothing ran.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class BreakingScriptMatchesTestPatternException : Exception
{
	internal BreakingScriptMatchesTestPatternException(string relativePath)
		: base($"File '{relativePath}' is named as a breaking script of the Code folder (breaking.sql, b.sql, *.breaking.sql, *.b.sql) and matches TestFilePatterns, so it would be both a breaking script and a test. Rename it.")
	{
		RelativePath = relativePath;
	}

	/// <summary>
	/// The relative path of the file.
	/// </summary>
	public string RelativePath { get; private set; }
}
