namespace DbLive.Schema;

/// <summary>
/// The kinds of objects a <see cref="SchemaSnapshot"/> holds. Some exist on one engine only.
/// </summary>
public enum SchemaObjectType
{
	Schema,
	Table,
	Column,
	PrimaryKey,
	UniqueConstraint,
	ForeignKey,
	CheckConstraint,
	/// <summary>PostgreSQL only.</summary>
	ExclusionConstraint,
	Index,
	View,
	/// <summary>PostgreSQL only.</summary>
	MaterializedView,
	Function,
	Procedure,
	Trigger,
	Type,
	Sequence,
	/// <summary>SQL Server only.</summary>
	Synonym,
	/// <summary>PostgreSQL only.</summary>
	Extension
}
