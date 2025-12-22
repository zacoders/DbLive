namespace DbLive.Project;

[ExcludeFromCodeCoverage]
public class CodeGroup
{
	public required string Path { get; set; }
	public IReadOnlyCollection<CodeItem> CodeItems { get; set; } = new List<CodeItem>();
}