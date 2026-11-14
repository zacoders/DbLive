/// <summary>
/// The public API files of a package ("The public API" in Development.md): PublicAPI.Shipped.txt, the API on main,
/// and PublicAPI.Unshipped.txt, where the code fix of RS0016 writes in a branch. One line per public symbol.
/// </summary>
static class ApiFile
{
	public const string Shipped = "PublicAPI.Shipped.txt";
	public const string Unshipped = "PublicAPI.Unshipped.txt";

	// the first line of both files: the nullable annotations count.
	const string Header = "#nullable enable";

	// a line of Unshipped that removes a symbol of Shipped.
	const string Removed = "*REMOVED*";

	/// <summary>The packages with a C# API: the folders of src/ that have PublicAPI.Shipped.txt, with '/'.</summary>
	public static List<string> Packages()
		=> Directory.GetDirectories("src")
			.Where(folder => File.Exists(Path.Combine(folder, Shipped)))
			.Select(folder => folder.Replace('\\', '/'))
			.Order(StringComparer.Ordinal)
			.ToList();

	/// <summary>The symbols of a file: its lines without the header and empty lines. None for no file.</summary>
	public static List<string> Symbols(string? text)
		=> text is null
			? []
			: text.Split('\n')
				.Select(line => line.TrimEnd('\r'))
				.Where(line => line.Trim().Length > 0 && !line.StartsWith('#'))
				.ToList();

	/// <summary>
	/// Moves the lines of PublicAPI.Unshipped.txt of <paramref name="package"/> to its PublicAPI.Shipped.txt: a symbol is
	/// added, a '*REMOVED*' line deletes its symbol. Unshipped keeps only the header. Returns the number of lines moved.
	/// </summary>
	public static int Move(string package)
	{
		string shippedPath = $"{package}/{Shipped}";
		string unshippedPath = $"{package}/{Unshipped}";
		string shippedText = File.ReadAllText(shippedPath);
		List<string> unshipped = Symbols(File.Exists(unshippedPath) ? File.ReadAllText(unshippedPath) : null);
		if (unshipped.Count == 0)
			return 0;

		var removed = unshipped
			.Where(line => line.StartsWith(Removed, StringComparison.Ordinal))
			.Select(line => line[Removed.Length..])
			.ToHashSet(StringComparer.Ordinal);
		List<string> shipped = Symbols(shippedText)
			.Where(symbol => !removed.Contains(symbol))
			.Concat(unshipped.Where(line => !line.StartsWith(Removed, StringComparison.Ordinal)))
			.Distinct(StringComparer.Ordinal)
			// the order of the code fix of RS0016.
			.Order(StringComparer.OrdinalIgnoreCase)
			.ThenBy(symbol => symbol, StringComparer.Ordinal)
			.ToList();

		string newLine = shippedText.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
		File.WriteAllText(shippedPath, string.Join(newLine, [Header, .. shipped]) + newLine);
		File.WriteAllText(unshippedPath, Header + newLine);
		return unshipped.Count;
	}
}
