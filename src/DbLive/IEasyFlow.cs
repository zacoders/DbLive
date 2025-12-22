namespace EasyFlow;

public interface IEasyFlow
{
	void Deploy(DeployParameters parameters);
	void DropDatabase(bool skipIfNotExists = true);
}