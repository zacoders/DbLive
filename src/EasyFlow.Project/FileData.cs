namespace EasyFlow.Project;

public class FileData
{
	private string _content = "";

	public required string Content
	{
		get => _content;
		init
		{
			Crc32Hash = value.Crc32HashCode();
			_content = value;
		}
	}

	public int Crc32Hash { get; private set; }

	public required string FilePath { get; init; }
	public required string RelativePath { get; init; }
}