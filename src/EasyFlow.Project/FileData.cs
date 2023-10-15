namespace EasyFlow.Project;

public class FileData
{
	private string _content = "";

	public required string Content { 
		get => _content; 
		init {
			MD5Hash = value.MD5HashCode();
			_content = value;
		}
	}

	public Guid MD5Hash { get; private set; }

	public required string FilePath { get; init; }
}