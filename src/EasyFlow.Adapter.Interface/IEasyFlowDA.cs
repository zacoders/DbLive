namespace EasyFlow.Adapter.Interface;

public interface IEasyFlowDA
{
	IReadOnlyCollection<MigrationDto> GetMigrations(string cnnString);
	bool EasyFlowInstalled(string cnnString);
}
