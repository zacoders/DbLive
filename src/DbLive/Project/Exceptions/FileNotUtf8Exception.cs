namespace DbLive;

/// <summary>
/// A file of the project (a script or a settings file) has bytes that are not UTF-8: it is saved in another encoding, such as
/// Windows-1252. Read as UTF-8, each of its non-ASCII characters would become U+FFFD and be deployed so.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class FileNotUtf8Exception : Exception
{
	internal FileNotUtf8Exception(string relativePath, string location, Exception innerException)
		: base(
			$"File '{relativePath}' of {location} is not UTF-8. DbLive reads the files of a project as UTF-8 "
			+ "(UTF-16 or UTF-32 only with a byte order mark), so each character of another encoding, such as Windows-1252, "
			+ "would be deployed as '�'. Save the file as UTF-8. "
			+ "If it is a migration or breaking script already applied to a database, converting it changes its checksum: "
			+ "see \"File encoding\" in the DbLive docs.",
			innerException
		)
	{
		RelativePath = relativePath;
	}

	public string RelativePath { get; private set; }
}
