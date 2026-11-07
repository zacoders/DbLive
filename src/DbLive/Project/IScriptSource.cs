namespace DbLive.Project;

/// <summary>
/// The files of a DbLive project.
/// </summary>
public interface IScriptSource
{
	/// <summary>
	/// Where the scripts come from, for logs and exception messages: "DemoMSSQL.dll" or the folder path.
	/// </summary>
	string Location { get; }

	/// <summary>
	/// Relative paths of all files, with '/': "Migrations/001.migration.sql".
	/// </summary>
	IReadOnlyList<string> GetFiles();

	/// <summary>
	/// Reads a file by its relative path from <see cref="GetFiles"/>.
	/// </summary>
	Task<string> ReadAsync(string relativePath);
}
