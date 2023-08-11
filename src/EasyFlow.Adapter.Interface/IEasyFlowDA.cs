namespace EasyFlow.Adapter;

public interface IEasyFlowDA
{
	IReadOnlyCollection<MigrationDto> GetMigrations(string cnnString);
}