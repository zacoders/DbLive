namespace DbLive;

public enum TranIsolationLevel
{
	ReadCommitted,
	RepeatableRead,
	Serializable,
	Snapshot
}