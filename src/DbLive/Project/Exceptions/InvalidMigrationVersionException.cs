namespace DbLive;

/// <summary>
/// The version of a file of the Migrations folder is not digits only, is larger than <see cref="long.MaxValue"/>, or is not a
/// <c>yyyyMMddHHmmss</c> timestamp when <c>EnforceTimestampVersion</c> is on. Also a file that is not a migration at all, such as
/// <c>config.json</c> or a macOS <c>._001.migration.sql</c>. Nothing ran.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class InvalidMigrationVersionException : Exception
{
	internal InvalidMigrationVersionException(string relativePath, string reason)
		: base($"Invalid migration version in file '{relativePath}'. {reason}")
	{
		RelativePath = relativePath;
		FileName = Path.GetFileName(relativePath);
	}

	/// <summary>
	/// The name of the file, without its folder.
	/// </summary>
	public string FileName { get; private set; }

	/// <summary>
	/// The relative path of the file.
	/// </summary>
	public string RelativePath { get; private set; }
}
