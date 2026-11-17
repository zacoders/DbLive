using System.IO.Hashing;
using System.Text;

namespace DbLive.Tests.Common;

/// <summary>
/// The content hash DbLive stored before line endings were normalized: XxHash64 of the raw file text.
/// </summary>
public static class LegacyContentHash
{
	public static long Of(string content) => unchecked((long)XxHash64.HashToUInt64(Encoding.UTF8.GetBytes(content)));
}
