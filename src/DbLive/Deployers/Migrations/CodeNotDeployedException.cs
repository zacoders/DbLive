namespace DbLive;

/// <summary>
/// The breaking phase was started before the normal deployment of the same build completed.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class CodeNotDeployedException : Exception
{
	internal CodeNotDeployedException(int count, IReadOnlyList<string> relativePaths)
		: base(
			$"Breaking changes are deployed only after the normal deployment of the same build, but {count} code file(s) are not deployed with their current content: "
			+ string.Join(", ", relativePaths)
			+ (count > relativePaths.Count ? ", ..." : "")
			+ ". Deploy with DeployCode = true (DeployParameters.Default) first, then deploy breaking changes."
		)
	{
		Count = count;
		RelativePaths = relativePaths;
	}

	/// <summary>
	/// Number of code files without an applied row with their current hash in <c>dblive.code</c>.
	/// </summary>
	public int Count { get; private set; }

	/// <summary>
	/// The first of these files, by relative path.
	/// </summary>
	public IReadOnlyList<string> RelativePaths { get; private set; }
}
