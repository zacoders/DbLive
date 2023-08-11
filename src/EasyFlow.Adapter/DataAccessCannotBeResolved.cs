namespace EasyFlow.Adapter;

[Serializable]
internal class DataAccessCannotBeResolved : Exception
{
	private DBEngine dbEngine;

	public DataAccessCannotBeResolved(DBEngine dbEngine)
		: base($"DataAccess cannot be created for '{dbEngine}'.")
	{
		this.dbEngine = dbEngine;
	}
}