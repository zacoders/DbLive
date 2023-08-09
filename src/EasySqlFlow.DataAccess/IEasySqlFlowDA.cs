namespace EasySqlFlow.DataAccess
{
	public interface IEasySqlFlowDA
	{
		IReadOnlyCollection<MigrationDto> GetMigrations(string cnnString);
	}
}