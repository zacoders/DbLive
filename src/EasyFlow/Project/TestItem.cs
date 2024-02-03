namespace EasyFlow.Project;

[ExcludeFromCodeCoverage]
public class TestItem
{
	public required string Name { get; set; }
	public required string Folder { get; set; }
	public FileData? InitFileData { get; set; }
	public required FileData FileData { get; set; }
}