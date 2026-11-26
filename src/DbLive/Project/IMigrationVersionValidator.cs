namespace DbLive.Project;

internal interface IMigrationVersionValidator
{
	/// <summary>
	/// Checks that the version part of the file name, as written, is a <c>yyyyMMddHHmmss</c> timestamp (<c>EnforceTimestampVersion</c>).
	/// </summary>
	/// <exception cref="InvalidMigrationVersionException">The version part is not such a timestamp.</exception>
	void Validate(string relativePath);
}
