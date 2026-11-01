namespace DbLive.Project;

[ExcludeFromCodeCoverage]
public class CodeGroup
{
	/// <summary>
	/// The folder of the group relative to the scripts root, with '/': "Code", "Code/dbo/functions".
	/// </summary>
	public required string Path { get; set; }
	public IReadOnlyCollection<CodeItem> CodeItems { get; set; } = [];
}