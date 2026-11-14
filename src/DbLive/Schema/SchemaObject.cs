namespace DbLive.Schema;

/// <summary>
/// One object of a <see cref="SchemaSnapshot"/>. <see cref="Name"/> identifies the object within its type,
/// <see cref="Definition"/> describes it in the engine's own terms: two snapshots of one database compare by text.
/// </summary>
internal sealed record SchemaObject
{
	internal SchemaObject(SchemaObjectType type, string name, string definition, string? parent = null)
	{
		Type = type;
		Name = name;
		Definition = definition;
		Parent = parent;
	}

	public SchemaObjectType Type { get; internal init; }

	public string Name { get; internal init; }

	public string Definition { get; internal init; }

	/// <summary>
	/// The name of the object it belongs to (the table of a column, the schema of a table), or null.
	/// </summary>
	public string? Parent { get; internal init; }
}
