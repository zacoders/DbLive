namespace DbLive.Common;

public class FileData
{
	private string _content = "";

	public required string Content
	{
		get => _content;
		init
		{
			ContentHash = value.Crc32HashCode();
			_content = value;
		}
	}

	public int ContentHash { get; private set; }

	public required string FilePath { get; init; }
	public required string RelativePath { get; init; }
	public string FileName => Path.GetFileName(FilePath);
}