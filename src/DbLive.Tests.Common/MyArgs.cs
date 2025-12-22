namespace DbLive.Tests.Common;

public static class MyArg
{
	public static ref IEnumerable<T> SequenceEqual<T>(IEnumerable<T> value)
	{
		return ref Arg.Is<IEnumerable<T>>(arr => arr.SequenceEqual(value));
	}
}
