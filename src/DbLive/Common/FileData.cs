namespace DbLive.Common;

internal class FileData
{
	private string _content = "";

	public required string Content
	{
		get => _content;
		init
		{
			ContentHash = value.ComputeFileHash();
			_content = value;
		}
	}

	public long ContentHash { get; private set; }

	/// <summary>
	/// Path relative to the scripts root, with '/': "Code/dbo/GetUser.sql".
	/// </summary>
	public required string RelativePath { get; init; }
	public string FileName => Path.GetFileName(RelativePath);

	/// <summary>
	/// True when a hash stored in the database belongs to this content with any line endings,
	/// including hashes stored by DbLive versions that hashed the raw file text. For migrations: an applied migration must keep
	/// matching. A code file compares only <see cref="ContentHash"/> and runs once more instead.
	/// </summary>
	public bool MatchesHash(long storedHash) =>
		storedHash == ContentHash || _content.ComputeLegacyFileHashes().Contains(storedHash);
}
