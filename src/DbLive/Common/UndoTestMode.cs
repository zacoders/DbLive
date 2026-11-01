namespace DbLive.Common;

internal enum UndoTestMode
{
	/// <summary>Every migration version is deployed once, as in production.</summary>
	None,

	/// <summary>
	/// Every migration version that has an undo script goes through the undo cycle before its final deployment:
	/// migration → undo (a rollback before the breaking phase), then, if the version has a breaking script,
	/// migration → breaking → breaking → undo (a rollback after it). The schema after each undo must equal the schema before the migration.
	/// </summary>
	UndoCycle
}
