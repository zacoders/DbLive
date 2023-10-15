using System.Security.Cryptography;
using System.Text;

namespace EasyFlow.Common;

public static class MD5Hasher
{

	public static Guid MD5HashCode(this string input)
	{
		using var hashAlgo = MD5.Create();
		byte[] inputBytes = Encoding.UTF8.GetBytes(input);
		byte[] hashBytes = hashAlgo.ComputeHash(inputBytes);
		return new Guid(hashBytes);
	}
}
