namespace DbLive.Project;

[ExcludeFromCodeCoverage]
public record GenericItem
{
	public required string Name { get; set; }
	public required FileData FileData { get; set; }
}