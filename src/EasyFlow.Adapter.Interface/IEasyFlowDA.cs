namespace EasyFlow.Adapter.Interface;

public interface IEasyFlowDA
{
	IReadOnlyCollection<MigrationDto> GetMigrations(string domain, string cnnString);
	bool EasyFlowInstalled(string cnnString);
}
