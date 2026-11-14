using System.Text;

namespace DbLive.Project;

/// <summary>
/// How the files of a project are decoded: UTF-8, or UTF-16 and UTF-32 with a byte order mark. The same for a folder and
/// for an assembly, so content hashes do not depend on where the files are read from.
/// </summary>
internal static class ScriptEncoding
{
	/// <summary>
	/// UTF-8 that throws <see cref="DecoderFallbackException"/> on bytes that are not UTF-8 (a file in Windows-1252 or
	/// Windows-1251), instead of replacing them with U+FFFD. The readers detect a byte order mark with it: with a UTF-16 or
	/// UTF-32 BOM they decode that encoding; after a UTF-8 BOM <see cref="StreamReader"/> takes <see cref="Encoding.UTF8"/>,
	/// which does not throw (an accepted limitation).
	/// </summary>
	public static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
}
