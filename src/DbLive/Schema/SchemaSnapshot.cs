namespace DbLive.Schema;

/// <summary>
/// The schema of a database read from its catalog by <see cref="IDbLiveInternalDA.GetSchemaSnapshotAsync"/>, without DbLive's own
/// <c>dblive</c> schema, object ids, current identity and sequence values and statistics.
/// Two snapshots are compared by <see cref="SchemaDiff.Compare"/>.
/// </summary>
internal sealed class SchemaSnapshot
{
	private readonly Dictionary<(SchemaObjectType Type, string Name), SchemaObject> _objects = [];

	internal SchemaSnapshot(IEnumerable<SchemaObject> objects)
	{
		foreach (SchemaObject obj in objects)
		{
			// two objects with one name, such as two check constraints of a table with one definition, both named by SQL Server:
			// the second one is numbered, so the snapshot keeps how many there are.
			string name = obj.Name;
			for (int i = 2; _objects.ContainsKey((obj.Type, name)); i++)
			{
				name = $"{obj.Name} #{i}";
			}

			_objects.Add((obj.Type, name), obj with { Name = name });
		}
	}

	public IReadOnlyCollection<SchemaObject> Objects => _objects.Values;

	public SchemaObject? Find(SchemaObjectType type, string name)
		=> _objects.GetValueOrDefault((type, name));
}
