namespace DbLive.Project;

[ExcludeFromCodeCoverage]
public class TestItem
{
	public required string Name { get; set; }
	public FileData? SetupFileData { get; set; }
	public required FileData FileData { get; set; }
}