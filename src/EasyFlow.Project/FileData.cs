namespace EasyFlow.Project;

public class FileData
{
	private string _content = "";

	public required string Content { 
		get => _content; 
		init {
			MD5Hash = value.Crc32HashCode();
			_content = value;
		}
	}

	public int MD5Hash { get; private set; }

	public required string FilePath { get; init; }
	public required string RelativePath { get; init; }
}