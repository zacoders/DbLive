namespace EasyFlow.Deploy
{
	public interface IEasyFlowDA
	{
		IReadOnlyCollection<MigrationDto> GetMigrations(string cnnString);
	}
}