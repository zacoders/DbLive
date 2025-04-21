
namespace EasyFlow.xunit;

public interface IEasyFlowDockerContainer
{
	Task StartAsync();
	string GetConnectionString();
	Task DisposeAsync();
}
