namespace DbLive.Testing;

/// <summary>
/// The schema check of the deployment test (<c>[SqlDeployFact]</c>) failed: a second breaking deployment changed the schema.
/// The message lists the differences.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class SchemaChangedException : Exception
{
	internal SchemaChangedException(string message, SchemaDiff diff)
		: base($"{message}{Environment.NewLine}{diff}")
	{
		Diff = diff;
	}

	internal SchemaDiff Diff { get; }
}
