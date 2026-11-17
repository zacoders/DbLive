namespace DbLive.Deployers.Code;

/// <summary>
/// The rows of <c>dblive.code</c>, read with one query instead of one per file, by the stored (lower-case) path.
/// </summary>
internal sealed class CodeRows(IEnumerable<CodeItemDto> rows)
{
	private readonly Dictionary<string, CodeItemDto> _rows = rows.ToDictionary(row => row.RelativePath, StringComparer.Ordinal);

	/// <summary>
	/// The file has an applied row with its current content: the code deployment skips it (<c>[hash match]</c>), and the guard
	/// of the breaking phase passes exactly when no code file is left to execute. A removed row is not deployed: the file came
	/// back, and the breaking phase may have dropped its object in between. Only the current hash counts, not the hashes of older
	/// DbLive versions (<see cref="FileData.MatchesHash"/>): a code file deployed by DbLive 1.x from a CRLF checkout runs once more,
	/// which is harmless for code, and its row gets the current hash.
	/// </summary>
	public bool IsDeployed(FileData fileData) =>
		_rows.TryGetValue(fileData.RelativePath.ToStoredPath(), out CodeItemDto? row)
		&& row.Status == CodeItemStatus.Applied
		&& row.ContentHash == fileData.ContentHash;
}
