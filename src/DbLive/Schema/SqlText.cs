using System.Text.RegularExpressions;

namespace DbLive.Schema;

internal static partial class SqlText
{
	/// <summary>
	/// Replaces each run of whitespace with one space and trims the text: the source of a procedure or a view, stored as written,
	/// does not differ by line endings or indentation.
	/// </summary>
	public static string CollapseWhitespace(string text)
		=> WhitespaceRegex().Replace(text, " ").Trim();

	[GeneratedRegex(@"\s+")]
	private static partial Regex WhitespaceRegex();
}
