namespace DbLive.Adapter;

/// <summary>
/// A row of the schema snapshot query of a provider; <c>ObjectType</c> is a <see cref="SchemaObjectType"/> name.
/// The query names its columns after the properties: Dapper maps them by name, and a column it does not match leaves the property empty.
/// </summary>
[ExcludeFromCodeCoverage]
internal class SchemaObjectRow
{
	public string ObjectType { get; init; } = "";
	public string ObjectName { get; init; } = "";
	public string? ParentName { get; init; }
	public string? Definition { get; init; }

	public SchemaObject ToSchemaObject() => new(
		Enum.Parse<SchemaObjectType>(ObjectType),
		ObjectName,
		Definition ?? "",
		ParentName
	);
}
