namespace DbLive.Adapter;

/// <summary>
/// The status of a <c>dblive.code</c> row. A failure writes nothing: the row of a code file stays <see cref="Applying"/>, the row of
/// a breaking script of the Code folder keeps its last success.
/// </summary>
internal enum CodeItemStatus
{
	Applied,

	/// <summary>
	/// DbLive started to deploy new content of the file and has not confirmed it: written before the code files run, on the applied
	/// rows of the files that run, and replaced by <see cref="Applied"/> when the file succeeds. A file whose run failed or was not
	/// confirmed keeps it and runs again on the next deployment whatever its content: the run may have changed the database (the
	/// batches of an In-Memory OLTP file commit one by one; the file committed and its row was not written). <c>content_hash</c> and
	/// <c>applied_utc</c> keep the last success.
	/// </summary>
	Applying,

	/// <summary>
	/// The file is no longer in the project. A file that comes back is executed again.
	/// </summary>
	Removed
}
