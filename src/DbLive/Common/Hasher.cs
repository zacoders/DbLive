using System.IO.Hashing;
using System.Text;

namespace DbLive.Common;

public static class HashExtensions
{
	public static long ComputeFileHash(this string input)
	{
		byte[] inputBytes = Encoding.UTF8.GetBytes(input);
		return unchecked((long)XxHash64.HashToUInt64(inputBytes));
	}
}
