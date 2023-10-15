namespace EasyFlow.Project;

public record CodeItem
{
	public required string Name { get; set; }
	public required FileData FileData { get; set; }
}