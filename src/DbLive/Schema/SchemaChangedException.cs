namespace DbLive.Schema;

/// <summary>
/// A schema check of the deployment tests failed: the message names the check and the script, and lists the differences.
/// </summary>
[ExcludeFromCodeCoverage]
public class SchemaChangedException(SchemaCheck check, string message, SchemaDiff diff)
	: Exception($"{message}{Environment.NewLine}{diff}")
{
	public SchemaCheck Check { get; } = check;

	public SchemaDiff Diff { get; } = diff;
}
