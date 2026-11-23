namespace DbLive.Adapter;

/// <summary>
/// The status of a <c>dblive.code</c> row. A failure writes no row: the row keeps the last success of the file.
/// </summary>
internal enum CodeItemStatus
{
	Applied,
	
	/// <summary>
	/// The file is no longer in the project. A file that comes back is executed again.
	/// </summary>
	Removed
}
