namespace EasySqlFlow.DataAccess
{
	public interface IEasyFlowDA
	{
		IReadOnlyCollection<MigrationDto> GetMigrations(string cnnString);
	}
}