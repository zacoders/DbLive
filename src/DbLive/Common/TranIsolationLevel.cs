namespace DbLive.Common;

public enum TranIsolationLevel
{
	[Obsolete("Chaos is deprecated and treated as an alias for ReadCommitted.")]
	Chaos,
	ReadCommitted,
	RepeatableRead,
	Serializable,
	Snapshot
}