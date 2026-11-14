namespace DbLive;

internal enum TranIsolationLevel
{
	ReadCommitted,
	RepeatableRead,
	Serializable,
	Snapshot
}