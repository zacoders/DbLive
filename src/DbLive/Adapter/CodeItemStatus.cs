namespace DbLive.Adapter;

internal enum CodeItemStatus
{
	Error,
	Applied,
	
	/// <summary>
	/// The file is no longer in the project. A file that comes back is executed again.
	/// </summary>
	Removed
}
