using System.IO.Hashing;
using System.Text;

namespace DbLive.Common;

public static class HashExtensions
{
	/// <summary>
	/// Hash of the file text with CRLF line endings replaced by LF,
	/// so a Windows (CRLF) and a Linux (LF) checkout of the same file have the same hash.
	/// </summary>
	public static long ComputeFileHash(this string input) => ComputeHash(input.ToLfLineEndings());

	/// <summary>
	/// Hashes that DbLive stored for the same file before line endings were normalized:
	/// the raw text of a CRLF checkout, and the raw text itself (for mixed line endings).
	/// The raw text of an LF checkout is the normalized text, see <see cref="ComputeFileHash"/>.
	/// </summary>
	internal static IEnumerable<long> ComputeLegacyFileHashes(this string input)
	{
		yield return ComputeHash(input.ToLfLineEndings().Replace("\n", "\r\n"));
		yield return ComputeHash(input);
	}

	// only CRLF, as git converts it; string.ReplaceLineEndings would also replace '\r', U+0085, U+2028 and U+2029.
	private static string ToLfLineEndings(this string input) => input.Replace("\r\n", "\n");

	private static long ComputeHash(string input)
	{
		byte[] inputBytes = Encoding.UTF8.GetBytes(input);
		return unchecked((long)XxHash64.HashToUInt64(inputBytes));
	}
}
