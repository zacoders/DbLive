namespace EasyFlow.Project;

public record CodeItem
{
	public required string Name { get; set; }
	public required Uri FileUri { get; set; }
}