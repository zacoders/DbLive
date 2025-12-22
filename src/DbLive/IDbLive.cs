namespace DbLive;

public interface IDbLive
{
	void Deploy(DeployParameters parameters);
	void DropDatabase(bool skipIfNotExists = true);
}