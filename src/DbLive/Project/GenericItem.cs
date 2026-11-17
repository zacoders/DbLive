namespace DbLive.Project;

[ExcludeFromCodeCoverage]
internal record GenericItem
{
	public required string Name { get; set; }
	public required FileData FileData { get; set; }
}