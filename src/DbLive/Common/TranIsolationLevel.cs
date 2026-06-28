namespace DbLive.Common;

public enum TranIsolationLevel
{
	ReadCommitted,
	RepeatableRead,
	Serializable,
	Snapshot
}