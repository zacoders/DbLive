
namespace DbLive.SelfDeployer;

public class InternalMigration
{
	public required int Version { get; set; }
	public required FileData FileData { get; set; }
	public string Name { get; set; } = string.Empty;
}
