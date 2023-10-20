using Force.Crc32;
using System.Text;

namespace EasyFlow.Common;

public static class Hasher
{
	public static int Crc32HashCode(this string input)
	{
		byte[] inputBytes = Encoding.UTF8.GetBytes(input);
		return (int)Crc32Algorithm.Compute(inputBytes);
	}
}
