namespace DbLive.Common;

public enum TranIsolationLevel
{
	Chaos,
	ReadCommitted,
	RepeatableRead,
	Serializable
}