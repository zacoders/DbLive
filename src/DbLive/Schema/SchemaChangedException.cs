namespace DbLive.Schema;

/// <summary>
/// The schema check of the deployment test (<c>[SqlDeployFact]</c>) failed: a second breaking deployment changed the schema.
/// The message lists the differences.
/// </summary>
[ExcludeFromCodeCoverage]
public class SchemaChangedException(string message, SchemaDiff diff)
	: Exception($"{message}{Environment.NewLine}{diff}")
{
	public SchemaDiff Diff { get; } = diff;
}
