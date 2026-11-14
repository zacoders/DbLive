
namespace DbLive.SelfDeployer;

internal class InternalMigration
{
	public required long Version { get; set; }
	public required FileData FileData { get; set; }
	public string Name { get; set; } = string.Empty;
}
