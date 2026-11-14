namespace DbLive.Adapter;

/// <summary>
/// The kind of a file of the Code folder that has a row in <c>dblive.code</c>.
/// </summary>
internal enum CodeItemType
{
	Code,
	/// <summary>
	/// A breaking script of the Code folder (<c>breaking.sql</c>, <c>*.breaking.sql</c>, <c>b.sql</c>, <c>*.b.sql</c>).
	/// </summary>
	Breaking
}
