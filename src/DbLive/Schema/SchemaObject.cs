namespace DbLive.Schema;

/// <summary>
/// One object of a <see cref="SchemaSnapshot"/>. <paramref name="Name"/> identifies the object within its type,
/// <paramref name="Definition"/> describes it in the engine's own terms: two snapshots of one database compare by text.
/// </summary>
/// <param name="Parent">The name of the object it belongs to (the table of a column, the schema of a table), or null.</param>
public sealed record SchemaObject(SchemaObjectType Type, string Name, string Definition, string? Parent = null);
