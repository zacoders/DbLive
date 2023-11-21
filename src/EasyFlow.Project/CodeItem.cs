namespace EasyFlow.Project;

[ExcludeFromCodeCoverage]
public record CodeItem
{
	public required string Name { get; set; }
	public required FileData FileData { get; set; }
}