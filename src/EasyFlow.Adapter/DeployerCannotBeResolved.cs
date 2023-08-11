namespace EasyFlow.Adapter;

[Serializable]
internal class DeployerCannotBeResolved : Exception
{
	private DBEngine dbEngine;

	public DeployerCannotBeResolved(DBEngine dbEngine)
		: base($"Deployer cannot be created for '{dbEngine}'.")
	{
		this.dbEngine = dbEngine;
	}
}
